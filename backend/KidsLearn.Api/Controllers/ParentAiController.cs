using System.Security.Claims;

public static class ParentAiController
{
    public static RouteGroupBuilder MapParentAiEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapPost("/ai/lessons/generate", async (IAiLessonGenerationService aiLessonGenerationService, ClaimsPrincipal user, GenerateAiLessonRequest request, CancellationToken cancellationToken) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await aiLessonGenerationService.GenerateAndPersistAsync(parentId.Value, request, cancellationToken);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        parentApi.MapPost("/ai/lessons/{lessonId:guid}/edit", async (IAiLessonEditingService aiLessonEditingService, ClaimsPrincipal user, Guid lessonId, EditAiLessonRequest request, CancellationToken cancellationToken) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await aiLessonEditingService.EditAsync(parentId.Value, lessonId, request, cancellationToken);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        return parentApi;
    }
}