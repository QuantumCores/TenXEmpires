using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Infrastructure.Filters
{
    /// <summary>
    /// Validates antiforgery token for API endpoints.
    /// Unlike the standard ValidateAntiForgeryToken, this works with Web API controllers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ValidateAntiForgeryTokenApiAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
                await next();
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new ObjectResult(new ApiErrorDto("CSRF_INVALID", "Invalid or missing CSRF token."))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}

