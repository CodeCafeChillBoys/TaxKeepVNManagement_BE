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

            var response = new ApiResponse<object>
            {
                Success = false
            };

            switch (exception)
            {
                case BadRequestException badRequestEx:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    response.ErrorCode = badRequestEx.ErrorCode;
                    response.Message = badRequestEx.Message;
                    break;
                case UnauthorizedException unauthorizedEx:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    response.ErrorCode = unauthorizedEx.ErrorCode;
                    response.Message = unauthorizedEx.Message;
                    break;
                case NotFoundException notFoundEx:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    response.ErrorCode = "DEPENDENT_NOT_FOUND";
                    response.Message = notFoundEx.Message;
                    break;
                case ForbiddenException forbiddenEx:
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    response.ErrorCode = "FORBIDDEN";
                    response.Message = forbiddenEx.Message;
                    break;
                default:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    response.ErrorCode = "STORAGE_UPLOAD_FAILED"; // Generic for this context or check message
                    response.Message = exception.Message;
                    break;
            }

            var result = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return context.Response.WriteAsync(result);
        }
    }
}
