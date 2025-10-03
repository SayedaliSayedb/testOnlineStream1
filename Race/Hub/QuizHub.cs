namespace WebApplication1.Race.Hub
{
    // Hubs/QuizHub.cs
    using Microsoft.AspNetCore.SignalR;
    using WebApplication1.Race.Services;

    public class QuizHub : Hub
    {
        private readonly IQuizDataService _dataService;
        private static readonly Dictionary<string, Participant> _participants = new();
        private static QuizState _currentState = new();
        private static bool _isInitialized = false;

        public QuizHub(IQuizDataService dataService)
        {
            _dataService = dataService;

            if (!_isInitialized)
            {
                InitializeFromStorage().Wait();
                _isInitialized = true;
            }
        }

        private async Task InitializeFromStorage()
        {
            var savedState = await _dataService.GetQuizStateAsync();
            if (savedState != null)
            {
                _currentState = savedState;
            }
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            await UpdateOnlineCount();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Mark participant as offline but keep in list
            var participant = _participants.Values.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (participant != null)
            {
                participant.IsOnline = false;
                participant.ConnectionId = string.Empty;
            }

            await base.OnDisconnectedAsync(exception);
            await UpdateOnlineCount();
            await UpdateParticipantsList();
        }

        // متد برای پیوستن مجری
        public async Task JoinAsHost(string hostName)
        {
            var host = new Participant
            {
                ConnectionId = Context.ConnectionId,
                UserId = "HOST",
                Name = hostName,
                ProfileImage = "",
                Hearts = 0,
                IsEliminated = false,
                IsOnline = true,
                JoinedAt = DateTime.Now,
                LastActivity = DateTime.Now
            };

            _participants["HOST"] = host;

            await Groups.AddToGroupAsync(Context.ConnectionId, "Hosts");
            await Clients.Caller.SendAsync("HostJoined", host);
            await UpdateParticipantsList();
        }

        // متد برای پیوستن شرکت‌کننده
        public async Task<Participant> JoinAsParticipant(string userId, string userName, string profileImage)
        {
            var existingParticipant = _participants.Values.FirstOrDefault(p => p.UserId == userId);

            if (existingParticipant != null)
            {
                // کاربر قبلاً وجود دارد - به‌روزرسانی اطلاعات
                existingParticipant.ConnectionId = Context.ConnectionId;
                existingParticipant.Name = userName;
                existingParticipant.ProfileImage = profileImage;
                existingParticipant.IsOnline = true;
                existingParticipant.LastActivity = DateTime.Now;

                // اگر مسابقه فعال است، وضعیت فعلی را برای کاربر ارسال کن
                await SendCurrentStateToParticipant(existingParticipant);
            }
            else
            {
                // کاربر جدید
                var participant = new Participant
                {
                    ConnectionId = Context.ConnectionId,
                    UserId = userId,
                    Name = userName,
                    ProfileImage = profileImage,
                    Hearts = _currentState.IsQuizActive ? 0 : (await _dataService.GetQuizSettingsAsync()).InitialHearts,
                    IsEliminated = false,
                    IsOnline = true,
                    JoinedAt = DateTime.Now,
                    LastActivity = DateTime.Now
                };

                _participants[userId] = participant;

                // اگر مسابقه فعال است، کاربر جدید فقط می‌تواند تماشا کند
                if (_currentState.IsQuizActive)
                {
                    participant.IsEliminated = true;
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, "Participants");
            await UpdateOnlineCount();
            await UpdateParticipantsList();

            var joinedParticipant = _participants[userId];
            await Clients.Caller.SendAsync("ParticipantJoined", joinedParticipant);

            return joinedParticipant;
        }

        private async Task SendCurrentStateToParticipant(Participant participant)
        {
            var quizData = await _dataService.GetQuizDataAsync();
            var settings = await _dataService.GetQuizSettingsAsync();

            await Clients.Client(participant.ConnectionId).SendAsync("QuizStateUpdated", new
            {
                IsQuizActive = _currentState.IsQuizActive,
                CurrentQuestionId = _currentState.CurrentQuestionId,
                IsQuestionActive = _currentState.IsQuestionActive,
                CommentsEnabled = settings.CommentsEnabled,
                ParticipantHearts = participant.Hearts,
                IsEliminated = participant.IsEliminated
            });

            if (_currentState.IsQuestionActive && _currentState.CurrentQuestionId >= 0)
            {
                var currentQuestion = quizData.Questions.FirstOrDefault(q => q.Id == _currentState.CurrentQuestionId);
                if (currentQuestion != null && !participant.IsEliminated)
                {
                    await Clients.Client(participant.ConnectionId).SendAsync("QuestionStarted", _currentState.CurrentQuestionId);
                }
            }
        }

        // شروع سوال
        public async Task StartQuestion(int questionId)
        {
            var quizData = await _dataService.GetQuizDataAsync();
            var question = quizData.Questions.FirstOrDefault(q => q.Id == questionId);

            if (question == null)
            {
                await Clients.Caller.SendAsync("Error", "سوال یافت نشد");
                return;
            }

            _currentState.CurrentQuestionId = questionId;
            _currentState.IsQuestionActive = true;
            _currentState.QuestionStartTime = DateTime.Now;
            _currentState.QuestionEndTime = DateTime.Now.AddSeconds(question.TimeLimit);

            await _dataService.SaveQuizStateAsync(_currentState);

            // ارسال سوال به شرکت‌کنندگان فعال
            foreach (var participant in _participants.Values.Where(p => !p.IsEliminated && p.IsOnline))
            {
                await Clients.Client(participant.ConnectionId).SendAsync("QuestionStarted", questionId);
            }

            await Clients.Group("Hosts").SendAsync("QuestionStarted", questionId);

            // شروع تایمر برای پایان خودکار سوال
            _ = StartQuestionTimer(question.TimeLimit, questionId);
        }

        private async Task StartQuestionTimer(int duration, int questionId)
        {
            await Task.Delay(duration * 1000);

            // اگر سوال هنوز فعال است، آن را پایان بده
            if (_currentState.IsQuestionActive && _currentState.CurrentQuestionId == questionId)
            {
                await EndQuestion();
            }
        }

        // پایان سوال
        public async Task EndQuestion()
        {
            if (!_currentState.IsQuestionActive)
                return;

            _currentState.IsQuestionActive = false;
            await _dataService.SaveQuizStateAsync(_currentState);

            var quizData = await _dataService.GetQuizDataAsync();
            var currentQuestion = quizData.Questions.FirstOrDefault(q => q.Id == _currentState.CurrentQuestionId);

            if (currentQuestion != null)
            {
                // ارسال پاسخ صحیح به همه
                await Clients.All.SendAsync("QuestionEnded", new
                {
                    QuestionId = _currentState.CurrentQuestionId,
                    CorrectAnswer = currentQuestion.CorrectAnswer,
                    Participants = _participants.Values.Where(p => !p.IsEliminated).Select(p => new
                    {
                        p.UserId,
                        p.Name,
                        p.Hearts,
                        HasAnswered = p.Answers.ContainsKey(_currentState.CurrentQuestionId)
                    }).ToList()
                });
            }

            await UpdateParticipantsList();
        }

        // ثبت پاسخ کاربر
        public async Task SubmitAnswer(int questionId, int answer)
        {
            var participant = _participants.Values.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (participant == null || participant.IsEliminated || !_currentState.IsQuestionActive)
                return;

            var quizData = await _dataService.GetQuizDataAsync();
            var question = quizData.Questions.FirstOrDefault(q => q.Id == questionId);

            if (question == null)
                return;

            var isCorrect = answer == question.CorrectAnswer;
            var responseTime = DateTime.Now - _currentState.QuestionStartTime;

            participant.Answers[questionId] = new ParticipantAnswer
            {
                QuestionId = questionId,
                SelectedAnswer = answer,
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.Now,
                ResponseTime = responseTime
            };

            if (!isCorrect)
            {
                participant.Hearts--;
                if (participant.Hearts <= 0)
                {
                    participant.IsEliminated = true;
                    await Clients.Client(participant.ConnectionId).SendAsync("Eliminated");
                }
            }

            // به‌روزرسانی وضعیت قلب کاربر
            await Clients.Client(participant.ConnectionId).SendAsync("HeartsUpdated", participant.Hearts);

            // اطلاع به مجری
            await Clients.Group("Hosts").SendAsync("ParticipantAnswered", new
            {
                Participant = participant,
                QuestionId = questionId,
                Answer = answer,
                IsCorrect = isCorrect,
                ResponseTime = responseTime.TotalSeconds
            });

            await UpdateParticipantsList();
        }

        // فعال/غیرفعال کردن کامنت‌ها
        public async Task EnableComments(bool enabled)
        {
            var settings = await _dataService.GetQuizSettingsAsync();
            settings.CommentsEnabled = enabled;
            await _dataService.SaveQuizSettingsAsync(settings);

            await Clients.All.SendAsync("CommentsToggled", enabled);
        }

        // ارسال پیام
        public async Task SendMessage(string message)
        {
            var participant = _participants.Values.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (participant == null)
                return;

            var settings = await _dataService.GetQuizSettingsAsync();
            if (!settings.CommentsEnabled && participant.UserId != "HOST")
                return;

            var chatMessage = new Message
            {
                UserId = participant.UserId,
                UserName = participant.Name,
                Content = message,
                SentAt = DateTime.Now,
                UserImage = participant.ProfileImage
            };

            await _dataService.SaveMessageAsync(chatMessage);
            await Clients.All.SendAsync("MessageReceived", chatMessage);
        }

        // شروع مجدد مسابقه
        public async Task RestartQuiz()
        {
            var settings = await _dataService.GetQuizSettingsAsync();

            // بازنشانی تمام شرکت‌کنندگان
            foreach (var participant in _participants.Values)
            {
                participant.Hearts = settings.InitialHearts;
                participant.IsEliminated = false;
                participant.Answers.Clear();
            }

            _currentState = new QuizState();
            await _dataService.SaveQuizStateAsync(_currentState);

            await Clients.All.SendAsync("QuizRestarted");
            await UpdateParticipantsList();
        }

        // بازگرداندن افراد حذف شده
        public async Task RestoreEliminated()
        {
            var settings = await _dataService.GetQuizSettingsAsync();

            foreach (var participant in _participants.Values.Where(p => p.IsEliminated))
            {
                participant.IsEliminated = false;
                participant.Hearts = settings.InitialHearts;
                await Clients.Client(participant.ConnectionId).SendAsync("Restored");
            }

            await Clients.All.SendAsync("EliminatedRestored");
            await UpdateParticipantsList();
        }

        // دریافت لیست شرکت‌کنندگان
        public async Task<List<Participant>> GetParticipants()
        {
            return _participants.Values.Where(p => p.UserId != "HOST").ToList();
        }

        // دریافت تعداد افراد آنلاین
        public async Task<int> GetOnlineCount()
        {
            return _participants.Values.Count(p => p.IsOnline && p.UserId != "HOST");
        }

        // به‌روزرسانی تعداد افراد آنلاین
        private async Task UpdateOnlineCount()
        {
            var onlineCount = await GetOnlineCount();
            await Clients.All.SendAsync("OnlineCountUpdated", onlineCount);
        }

        // به‌روزرسانی لیست شرکت‌کنندگان
        private async Task UpdateParticipantsList()
        {
            var participants = await GetParticipants();
            await Clients.Group("Hosts").SendAsync("ParticipantsUpdated", participants);
        }

        // شروع مسابقه
        public async Task StartQuiz()
        {
            _currentState.IsQuizActive = true;
            await _dataService.SaveQuizStateAsync(_currentState);

            await Clients.All.SendAsync("QuizStarted");
        }

        // پایان مسابقه
        public async Task EndQuiz()
        {
            _currentState.IsQuizActive = false;
            _currentState.IsQuestionActive = false;
            await _dataService.SaveQuizStateAsync(_currentState);

            await Clients.All.SendAsync("QuizEnded");
        }

        // دریافت وضعیت فعلی مسابقه
        public async Task<object> GetQuizState()
        {
            var settings = await _dataService.GetQuizSettingsAsync();
            var participants = await GetParticipants();

            return new
            {
                IsQuizActive = _currentState.IsQuizActive,
                CurrentQuestionId = _currentState.CurrentQuestionId,
                IsQuestionActive = _currentState.IsQuestionActive,
                CommentsEnabled = settings.CommentsEnabled,
                OnlineCount = participants.Count(p => p.IsOnline),
                ActiveParticipants = participants.Count(p => !p.IsEliminated),
                EliminatedParticipants = participants.Count(p => p.IsEliminated)
            };
        }
    }
}
