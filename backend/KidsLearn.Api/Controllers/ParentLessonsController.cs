using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class ParentLessonsController
{
    public static RouteGroupBuilder MapParentLessonsEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapPost("/lessons", async (AppDbContext db, ClaimsPrincipal user, CreateLessonRequest request) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Title)
                || string.IsNullOrWhiteSpace(request.Subject)
                || string.IsNullOrWhiteSpace(request.Topic)
                || request.Grade is < 1 or > 12)
            {
                return Results.BadRequest(new { error = "Title, subject, topic and grade (1-12) are required." });
            }

            if (request.Questions is null || request.Questions.Count == 0)
            {
                return Results.BadRequest(new { error = "At least one question is required." });
            }

            var lesson = new Lesson
            {
                Title = request.Title.Trim(),
                Subject = request.Subject.Trim(),
                Grade = request.Grade,
                Topic = request.Topic.Trim(),
                Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim(),
                CreatedBy = parentId.Value,
                CreatedAt = DateTime.UtcNow
            };

            for (var i = 0; i < request.Questions.Count; i++)
            {
                var sourceQuestion = request.Questions[i];
                if (string.IsNullOrWhiteSpace(sourceQuestion.QuestionText)
                    || sourceQuestion.Answers is null
                    || sourceQuestion.Answers.Count < 2)
                {
                    return Results.BadRequest(new { error = "Each question must have text and at least two answers." });
                }

                if (!sourceQuestion.Answers.Any(x => x.IsCorrect))
                {
                    return Results.BadRequest(new { error = "Each question must include at least one correct answer." });
                }

                var question = new Question
                {
                    QuestionText = sourceQuestion.QuestionText.Trim(),
                    Explanation = sourceQuestion.Explanation?.Trim() ?? string.Empty,
                    Order = sourceQuestion.Order ?? (i + 1)
                };

                for (var answerIndex = 0; answerIndex < sourceQuestion.Answers.Count; answerIndex++)
                {
                    var sourceAnswer = sourceQuestion.Answers[answerIndex];
                    if (string.IsNullOrWhiteSpace(sourceAnswer.AnswerText))
                    {
                        return Results.BadRequest(new { error = "Answer text is required." });
                    }

                    question.Answers.Add(new AnswerOption
                    {
                        AnswerText = sourceAnswer.AnswerText.Trim(),
                        IsCorrect = sourceAnswer.IsCorrect,
                        Order = sourceAnswer.Order ?? (answerIndex + 1)
                    });
                }

                lesson.Questions.Add(question);
            }

            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            var response = new LessonSummaryResponse(
                lesson.Id,
                lesson.Title,
                lesson.Subject,
                lesson.Grade,
                lesson.Topic,
                lesson.Difficulty,
                lesson.CreatedAt,
                lesson.Questions.Count);

            return Results.Created($"/api/v1/lessons/{lesson.Id}", response);
        });

        parentApi.MapGet("/lessons", async (AppDbContext db, ClaimsPrincipal user, string? subject, int? grade, string? topic, int page = 1, int pageSize = 20) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = db.Lessons
                .AsNoTracking()
                .Where(x => x.CreatedBy == parentId.Value);

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
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var lesson = await db.Lessons
                .AsNoTracking()
                .Include(x => x.Questions.OrderBy(q => q.Order))
                .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
                .FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId.Value);

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
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var sourceLesson = await db.Lessons
                .AsNoTracking()
                .Include(x => x.Questions.OrderBy(q => q.Order))
                .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
                .FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId.Value);

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
                CreatedBy = parentId.Value,
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
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId.Value);
            if (lesson is null)
            {
                return Results.NotFound();
            }

            if (request.Title is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return Results.BadRequest(new { error = "Title cannot be empty." });
                }

                lesson.Title = request.Title.Trim();
            }

            if (request.Subject is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Subject))
                {
                    return Results.BadRequest(new { error = "Subject cannot be empty." });
                }

                lesson.Subject = request.Subject.Trim();
            }

            if (request.Topic is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Topic))
                {
                    return Results.BadRequest(new { error = "Topic cannot be empty." });
                }

                lesson.Topic = request.Topic.Trim();
            }

            if (request.Difficulty is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Difficulty))
                {
                    return Results.BadRequest(new { error = "Difficulty cannot be empty." });
                }

                lesson.Difficulty = request.Difficulty.Trim();
            }

            if (request.Grade.HasValue)
            {
                if (request.Grade.Value is < 1 or > 12)
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
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId.Value);
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
