using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AwsPractice.AspireApp.ApiService.Extensions;
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // customize the response based on the exception type
        // httpContext.Response.StatusCode = exception switch
        // {
        //     ApplicationException => StatusCodes.Status400BadRequest,
        //     _ => StatusCodes.Status500InternalServerError
        // };
        
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext{
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = exception.GetType().Name,
                Title = "An error occurred while processing your request.",
                Detail = exception.Message
            }
        });
    }
}
