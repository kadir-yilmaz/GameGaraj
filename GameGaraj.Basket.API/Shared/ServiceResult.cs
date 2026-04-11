using System.Net;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Basket.API.Shared;

public interface IRequestByServiceResult<T> : IRequest<ServiceResult<T>>;

public interface IRequestByServiceResult : IRequest<ServiceResult>;

public class ServiceResult
{
    [JsonIgnore] public HttpStatusCode Status { get; set; }

    public ProblemDetails? Fail { get; set; }

    [JsonIgnore] public bool IsSuccess => Fail is null;
    [JsonIgnore] public bool IsFail => !IsSuccess;


    // Static factory methods
    public static ServiceResult SuccessAsNoContent()
    {
        return new ServiceResult
        {
            Status = HttpStatusCode.NoContent
        };
    }

    public static ServiceResult ErrorAsNotFound()
    {
        return new ServiceResult
        {
            Status = HttpStatusCode.NotFound,
            Fail = new ProblemDetails
            {
                Title = "Not Found",
                Detail = "The requested resource was not found"
            }
        };
    }


    public static ServiceResult Error(ProblemDetails problemDetails, HttpStatusCode status)
    {
        return new ServiceResult
        {
            Status = status,
            Fail = problemDetails
        };
    }

    public static ServiceResult Error(string title, string description, HttpStatusCode status)
    {
        return new ServiceResult
        {
            Status = status,
            Fail = new ProblemDetails
            {
                Title = title,
                Detail = description,
                Status = status.GetHashCode()
            }
        };
    }

    public static ServiceResult Error(string title, HttpStatusCode status)
    {
        return new ServiceResult
        {
            Status = status,
            Fail = new ProblemDetails
            {
                Title = title,
                Status = status.GetHashCode()
            }
        };
    }

    public static ServiceResult ErrorFromValidation(IDictionary<string, object?> errors)
    {
        return new ServiceResult
        {
            Status = HttpStatusCode.BadRequest,
            Fail = new ProblemDetails
            {
                Title = "Validation errors occured",
                Detail = "Please check the errors property for more details",
                Extensions = errors,
                Status = HttpStatusCode.BadRequest.GetHashCode()
            }
        };
    }
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    [JsonIgnore] public string? UrlAsCreated { get; set; }

    public static ServiceResult<T> SuccessAsOk(T data)
    {
        return new ServiceResult<T>
        {
            Status = HttpStatusCode.OK,
            Data = data
        };
    }

    //201 => Created => respones body header => location== api/products/5
    public static ServiceResult<T> SuccessAsCreated(T data, string url)
    {
        return new ServiceResult<T>
        {
            Status = HttpStatusCode.Created,
            Data = data,
            UrlAsCreated = url
        };
    }

    public new static ServiceResult<T> Error(ProblemDetails problemDetails, HttpStatusCode status)
    {
        return new ServiceResult<T>
        {
            Status = status,
            Fail = problemDetails
        };
    }

    public new static ServiceResult<T> Error(string title, string description, HttpStatusCode status)
    {
        return new ServiceResult<T>
        {
            Status = status,
            Fail = new ProblemDetails
            {
                Title = title,
                Detail = description,
                Status = status.GetHashCode()
            }
        };
    }

    public new static ServiceResult<T> Error(string title, HttpStatusCode status)
    {
        return new ServiceResult<T>
        {
            Status = status,
            Fail = new ProblemDetails
            {
                Title = title,
                Status = status.GetHashCode()
            }
        };
    }

    public new static ServiceResult<T> ErrorFromValidation(IDictionary<string, object?> errors)
    {
        return new ServiceResult<T>
        {
            Status = HttpStatusCode.BadRequest,
            Fail = new ProblemDetails
            {
                Title = "Validation errors occured",
                Detail = "Please check the errors property for more details",
                Extensions = errors,
                Status = HttpStatusCode.BadRequest.GetHashCode()
            }
        };
    }
}
