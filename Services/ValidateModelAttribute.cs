using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApplication1.Services
{
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(ms => ms.Value.Errors.Any())
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                context.Result = new BadRequestObjectResult(new
                {
                    success = false,
                    message = "داده‌های ورودی معتبر نیستند",
                    errors = errors
                });
            }
        }
    }

    public class LogActionAttribute : ActionFilterAttribute
    {
        private readonly ILogger<LogActionAttribute> _logger;

        public LogActionAttribute(ILogger<LogActionAttribute> logger)
        {
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            _logger.LogInformation(
                "Action {ActionName} executing with arguments {@Arguments}",
                context.ActionDescriptor.DisplayName,
                context.ActionArguments
            );

            base.OnActionExecuting(context);
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception == null)
            {
                _logger.LogInformation(
                    "Action {ActionName} executed successfully",
                    context.ActionDescriptor.DisplayName
                );
            }
            else
            {
                _logger.LogError(
                    context.Exception,
                    "Action {ActionName} failed",
                    context.ActionDescriptor.DisplayName
                );
            }

            base.OnActionExecuted(context);
        }
    }
}
