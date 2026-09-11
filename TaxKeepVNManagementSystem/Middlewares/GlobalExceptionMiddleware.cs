using Microsoft.AspNetCore.Http;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Exceptions;

namespace TaxKeepVNManagementSystem.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public GlobalExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            string errorCode;
            string message;

            switch (exception)
            {
                case BadRequestException badRequestEx:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    errorCode = badRequestEx.ErrorCode;
                    message = badRequestEx.Message;
                    break;

                case UnauthorizedException unauthorizedEx:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    errorCode = unauthorizedEx.ErrorCode;
                    message = unauthorizedEx.Message;
                    break;

                case ConflictException conflictEx:
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    errorCode = conflictEx.ErrorCode;
                    message = conflictEx.Message;
                    break;

                case NotFoundException notFoundEx:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    errorCode = "NOT_FOUND";
                    message = notFoundEx.Message;
                    break;

                case ForbiddenException forbiddenEx:
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    errorCode = "FORBIDDEN";
                    message = forbiddenEx.Message;
                    break;

                default:
                    Console.WriteLine($"[GLOBAL ERROR] {exception}");
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    errorCode = "INTERNAL_SERVER_ERROR";
                    message = exception.InnerException?.Message ?? exception.Message;
                    break;
            }

            var response = ApiResponse<object>.Fail(errorCode, message);
            var result = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return context.Response.WriteAsync(result);
        }
    }
}
