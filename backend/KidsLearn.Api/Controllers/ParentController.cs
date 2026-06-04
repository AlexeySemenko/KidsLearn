public static class ParentController
{
    public static RouteGroupBuilder MapParentController(this RouteGroupBuilder apiV1)
    {
        var parentApi = apiV1.MapGroup("")
            .RequireAuthorization("ParentOnly");

        parentApi.MapParentAiEndpoints();
        parentApi.MapParentChildrenEndpoints();
        parentApi.MapParentLessonsEndpoints();
        parentApi.MapParentAssignmentsEndpoints();
        parentApi.MapParentReportsEndpoints();

        return apiV1;
    }
}
