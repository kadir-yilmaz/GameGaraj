using System.ComponentModel.DataAnnotations;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Behaviors;

public class ValidationBehavior<TRequest, TResponse>(IServiceProvider serviceProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : ServiceResult
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext(request, serviceProvider, null);
        var validationResults = new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(request, context, validationResults, true);

        if (!isValid)
        {
            var errors = validationResults
                .GroupBy(x => x.MemberNames.FirstOrDefault() ?? string.Empty)
                .ToDictionary(
                    g => g.Key,
                    g => (object?)g.Select(x => x.ErrorMessage).ToArray()
                );

            // Create Error response via reflection or known type since TResponse is ServiceResult or ServiceResult<T>
            // Since ServiceResult has static factory method ErrorFromValidation
            
            // Check if TResponse is generic ServiceResult<T>
            if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(ServiceResult<>))
            {
                // Invoke ServiceResult<T>.ErrorFromValidation(errors)
                var method = typeof(TResponse).GetMethod("ErrorFromValidation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                if (method != null)
                {
                    return (TResponse)method.Invoke(null, [errors])!;
                }
            }
            
            // Fallback for non-generic ServiceResult
            return (TResponse)ServiceResult.ErrorFromValidation(errors);
        }

        return await next();
    }
}
