using Microsoft.AspNetCore.Mvc;
using TaskManager.Application.Common;

namespace TaskManager.Api.Extensions;

/// <summary>
/// Maps a failed Result&lt;T&gt; to the appropriate IActionResult.
/// Success cases are handled explicitly by each controller action.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToErrorResult<T>(this Result<T> result) =>
        result.Kind switch
        {
            ResultKind.Validation   => new UnprocessableEntityObjectResult(new { error = result.Error }),
            ResultKind.NotFound     => new NotFoundObjectResult(new { error = result.Error }),
            ResultKind.Conflict     => new ConflictObjectResult(new { error = result.Error }),
            ResultKind.Unauthorized => new UnauthorizedObjectResult(new { error = result.Error }),
            ResultKind.Forbidden    => new ObjectResult(new { error = result.Error }) { StatusCode = 403 },
            _ => throw new InvalidOperationException($"Cannot convert ResultKind.{result.Kind} to an error response.")
        };
}
