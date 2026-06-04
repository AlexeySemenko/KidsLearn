using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class ParentLessonsController
{
    public static RouteGroupBuilder MapParentLessonsEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapPost("/lessons", async (ISender sender, ClaimsPrincipal user, CreateLessonRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new CreateParentLessonCommand(parentId, request));
            if (result.Lesson is null)
            {
                return result.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                    _ => Results.Problem(result.Error ?? "Unexpected error.")
                };
            }

            return Results.Created($"/api/v1/lessons/{result.Lesson.Id}", result.Lesson);
        });

        parentApi.MapGet("/lessons", async (AppDbContext db, ClaimsPrincipal user, string? subject, int? grade, string? topic, int page = 1, int pageSize = 20) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = db.Lessons
                .AsNoTracking()
                .Where(x => x.CreatedBy == parentId);

            if (!string.IsNullOrWhiteSpace(subject))
            {
                query = query.Where(x => x.Subject == subject.Trim());
            }

            if (grade.HasValue)
            {
                query = query.Where(x => x.Grade == grade.Value);
            }

            if (!string.IsNullOrWhiteSpace(topic))
            {
                query = query.Where(x => x.Topic.Contains(topic.Trim()));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new LessonSummaryResponse(
                    x.Id,
                    x.Title,
                    x.Subject,
                    x.Grade,
                    x.Topic,
                    x.Difficulty,
                    x.CreatedAt,
                    x.Questions.Count))
                .ToListAsync();

            return Results.Ok(new { items, total, page, pageSize });
        });

        parentApi.MapGet("/lessons/{lessonId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid lessonId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var lesson = await db.Lessons
                .AsNoTracking()
                .Include(x => x.Questions.OrderBy(q => q.Order))
                .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
                .FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId);

            if (lesson is null)
            {
                return Results.NotFound();
            }

            var response = new LessonDetailResponse(
                lesson.Id,
                lesson.Title,
                lesson.Subject,
                lesson.Grade,
                lesson.Topic,
                lesson.Difficulty,
                lesson.CreatedAt,
                lesson.Questions
                    .OrderBy(q => q.Order)
                    .Select(q => new QuestionResponse(
                        q.Id,
                        q.QuestionText,
                        q.Explanation,
                        q.Order,
                        q.Answers
                            .OrderBy(a => a.Order)
                            .Select(a => new AnswerOptionResponse(a.Id, a.AnswerText, a.IsCorrect, a.Order))
                            .ToList()))
                    .ToList());

            return Results.Ok(response);
        });

        parentApi.MapPost("/lessons/{lessonId:guid}/duplicate", async (AppDbContext db, ClaimsPrincipal user, Guid lessonId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var sourceLesson = await db.Lessons
                .AsNoTracking()
                .Include(x => x.Questions.OrderBy(q => q.Order))
                .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
                .FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId);

            if (sourceLesson is null)
            {
                return Results.NotFound();
            }

            var duplicatedLesson = new Lesson
            {
                Title = $"{sourceLesson.Title} (Copy)",
                Subject = sourceLesson.Subject,
                Grade = sourceLesson.Grade,
                Topic = sourceLesson.Topic,
                Difficulty = sourceLesson.Difficulty,
                CreatedBy = parentId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var sourceQuestion in sourceLesson.Questions.OrderBy(q => q.Order))
            {
                var duplicatedQuestion = new Question
                {
                    QuestionText = sourceQuestion.QuestionText,
                    Explanation = sourceQuestion.Explanation,
                    Order = sourceQuestion.Order
                };

                foreach (var sourceAnswer in sourceQuestion.Answers.OrderBy(a => a.Order))
                {
                    duplicatedQuestion.Answers.Add(new AnswerOption
                    {
                        AnswerText = sourceAnswer.AnswerText,
                        IsCorrect = sourceAnswer.IsCorrect,
                        Order = sourceAnswer.Order
                    });
                }

                duplicatedLesson.Questions.Add(duplicatedQuestion);
            }

            db.Lessons.Add(duplicatedLesson);
            await db.SaveChangesAsync();

            var response = new LessonDetailResponse(
                duplicatedLesson.Id,
                duplicatedLesson.Title,
                duplicatedLesson.Subject,
                duplicatedLesson.Grade,
                duplicatedLesson.Topic,
                duplicatedLesson.Difficulty,
                duplicatedLesson.CreatedAt,
                duplicatedLesson.Questions
                    .OrderBy(q => q.Order)
                    .Select(q => new QuestionResponse(
                        q.Id,
                        q.QuestionText,
                        q.Explanation,
                        q.Order,
                        q.Answers
                            .OrderBy(a => a.Order)
                            .Select(a => new AnswerOptionResponse(a.Id, a.AnswerText, a.IsCorrect, a.Order))
                            .ToList()))
                    .ToList());

            return Results.Created($"/api/v1/lessons/{duplicatedLesson.Id}", response);
        });

        parentApi.MapPatch("/lessons/{lessonId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid lessonId, UpdateLessonRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId);
            if (lesson is null)
            {
                return Results.NotFound();
            }

            if (request.Title is not null)
            {
                var titleValidation = ApiEndpointHelpers.ValidateOptionalNonEmpty(request.Title, "Title cannot be empty.");
                if (titleValidation is not null)
                {
                    return titleValidation;
                }

                lesson.Title = request.Title.Trim();
            }

            if (request.Subject is not null)
            {
                var subjectValidation = ApiEndpointHelpers.ValidateOptionalNonEmpty(request.Subject, "Subject cannot be empty.");
                if (subjectValidation is not null)
                {
                    return subjectValidation;
                }

                lesson.Subject = request.Subject.Trim();
            }

            if (request.Topic is not null)
            {
                var topicValidation = ApiEndpointHelpers.ValidateOptionalNonEmpty(request.Topic, "Topic cannot be empty.");
                if (topicValidation is not null)
                {
                    return topicValidation;
                }

                lesson.Topic = request.Topic.Trim();
            }

            if (request.Difficulty is not null)
            {
                var difficultyValidation = ApiEndpointHelpers.ValidateOptionalNonEmpty(request.Difficulty, "Difficulty cannot be empty.");
                if (difficultyValidation is not null)
                {
                    return difficultyValidation;
                }

                lesson.Difficulty = request.Difficulty.Trim();
            }

            if (request.Grade.HasValue)
            {
                if (!ApiEndpointHelpers.IsGradeInRange(request.Grade.Value))
                {
                    return Results.BadRequest(new { error = "Grade must be between 1 and 12." });
                }

                lesson.Grade = request.Grade.Value;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new LessonSummaryResponse(
                lesson.Id,
                lesson.Title,
                lesson.Subject,
                lesson.Grade,
                lesson.Topic,
                lesson.Difficulty,
                lesson.CreatedAt,
                await db.Questions.CountAsync(x => x.LessonId == lesson.Id)));
        });

        parentApi.MapDelete("/lessons/{lessonId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid lessonId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId);
            if (lesson is null)
            {
                return Results.NotFound();
            }

            var hasAssignments = await db.Assignments.AnyAsync(x => x.LessonId == lesson.Id);
            if (hasAssignments)
            {
                return Results.Conflict(new { error = "Cannot delete a lesson with assignments." });
            }

            db.Lessons.Remove(lesson);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return parentApi;
    }
}


