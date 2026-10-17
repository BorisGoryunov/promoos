using FluentAssertions;
using Promoos.App.Dto;
using Promoos.Core;
using Promoos.Tests.Dto;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Promoos.Tests;

[Collection(ApiCollection.Name)]
public class TasksApiTests
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFixture _f;

    public TasksApiTests(ApiFixture f)
    {
        _f = f;
    }

    // ---------- helpers ----------

    private async Task<Guid> CreateTaskAsync(string title)
    {
        var res = await _f.Client.PostAsJsonAsync("/tasks", new { title }, _json);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        // Location: /tasks/{id}
        res.Headers.Location.Should().NotBeNull();
        res.Headers.Location!.OriginalString.Should().StartWith("/tasks/");

        // body: guid (сериализуется как строка)
        var id = await res.Content.ReadFromJsonAsync<Guid>(_json);
        id.Should().NotBe(Guid.Empty);

        // Location должен совпадать с телом
        res.Headers.Location!.OriginalString.Should().EndWith(id.ToString());
        return id;
    }

    private async Task<List<TaskDto>> GetTasksAsync(int limit = 100, int offset = 0)
    {
        var res = await _f.Client.GetAsync($"/tasks?limit={limit}&offset={offset}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<TaskDto>>(_json))!;
    }

    // --- GET /tasks ---

    [Fact]
    public async Task Get_tasks_returns_empty_list_when_db_is_empty()
    {
        var ct = TestContext.Current.CancellationToken;

        await _f.ResetAsync();

        var res = await _f.Client.GetAsync($"/tasks?limit={100}&offset={0}", ct);
        res.EnsureSuccessStatusCode();
        var list = (await res.Content.ReadFromJsonAsync<List<TaskDto>>(ct))!;

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_tasks_respects_limit_and_offset()
    {
        await _f.ResetAsync();

        var prefix = $"paging-{Guid.NewGuid():N}";
        for (var i = 0; i < 3; i++)
        {
            await CreateTaskAsync($"{prefix}-{i}");
        }

        var page = await GetTasksAsync(limit: 2, offset: 1);

        page.Should().HaveCount(2);
    }

    // --- POST /tasks ---

    [Fact]
    public async Task Post_task_returns_201_with_location_and_id()
    {
        var ct = TestContext.Current.CancellationToken;
        await _f.ResetAsync();

        var title = $"created-{Guid.NewGuid():N}";
        var res = await _f.Client.PostAsJsonAsync("/tasks", new { title }, _json, ct);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        res.Headers.Location.Should().NotBeNull();
        res.Headers.Location!.OriginalString.Should().StartWith("/tasks/");

        var id = await res.Content.ReadFromJsonAsync<Guid>(_json, ct);
        id.Should().NotBe(Guid.Empty);
        res.Headers.Location!.OriginalString.Should().Be($"/tasks/{id}");
    }

    [Fact]
    public async Task Post_task_creates_and_visible_in_list()
    {
        await _f.ResetAsync();

        var title = $"visible-{Guid.NewGuid():N}";
        var id = await CreateTaskAsync(title);

        var list = await GetTasksAsync();
        var task = list.Should().ContainSingle(t => t.TaskId == id).Subject;

        task.Title.Should().Be(title);
        task.IsCompleted.Should().BeFalse();
        task.CompletedAt.Should().BeNull();
    }


    // --- PUT /tasks/{id}/complete ---

    [Fact]
    public async Task Put_complete_sets_is_completed_and_completed_at()
    {
        var ct = TestContext.Current.CancellationToken;

        await _f.ResetAsync();

        var id = await CreateTaskAsync($"complete-{Guid.NewGuid():N}");

        var res = await _f.Client.PutAsync($"/tasks/{id}/complete", content: null, ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var task = (await GetTasksAsync()).Single(t => t.TaskId == id);
        task.IsCompleted.Should().BeTrue();
        task.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Put_complete_unknown_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;

        await _f.ResetAsync();

        var res = await _f.Client.PutAsync($"/tasks/{Guid.NewGuid()}/complete", content: null, ct);

        // GlobalExceptionHandler + ProblemDetails должны дать 404.
        // Если сейчас 500 — тест сигнализирует, что маппинга нет.
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- DELETE /tasks/{id} ---

    [Fact]
    public async Task Delete_removes_task()
    {
        var ct = TestContext.Current.CancellationToken;

        await _f.ResetAsync();

        var id = await CreateTaskAsync($"delete-{Guid.NewGuid():N}");

        var res = await _f.Client.DeleteAsync($"/tasks/{id}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await GetTasksAsync();
        list.Should().NotContain(t => t.TaskId == id);
    }

    [Fact]
    public async Task Delete_unknown_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;

        await _f.ResetAsync();

        var res = await _f.Client.DeleteAsync($"/tasks/{Guid.NewGuid()}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Полный жизненный цикл в одном тесте ---

    [Fact]
    public async Task Full_lifecycle_create_list_complete_delete()
    {
        var ct = TestContext.Current.CancellationToken;

        await _f.ResetAsync();

        // create → 201 + id
        var title = $"lifecycle-{Guid.NewGuid():N}";
        var id = await CreateTaskAsync(title);

        // list → видно, не завершена
        var created = (await GetTasksAsync()).Single(t => t.TaskId == id);
        created.IsCompleted.Should().BeFalse();

        // complete → 200, флаг и время
        (await _f.Client.PutAsync($"/tasks/{id}/complete", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var completed = (await GetTasksAsync()).Single(t => t.TaskId == id);
        completed.IsCompleted.Should().BeTrue();
        completed.CompletedAt.Should().NotBeNull();

        // delete → 200, задачи нет
        (await _f.Client.DeleteAsync($"/tasks/{id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetTasksAsync()).Should().NotContain(t => t.TaskId == id);
    }

    [Fact]
    public async Task Put_complete_already_completed_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        await _f.ResetAsync();

        var id = await CreateTaskAsync($"conflict-{Guid.NewGuid():N}");

        // первый вызов — успех
        var first = await _f.Client.PutAsync($"/tasks/{id}/complete", content: null, ct);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // второй вызов — задача уже завершена → 409
        var second = await _f.Client.PutAsync($"/tasks/{id}/complete", content: null, ct);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // ProblemDetails должен содержать 409
        if (second.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            var problem = await second.Content.ReadFromJsonAsync<ProblemDetailsDto>(ct);
            problem!.Status.Should().Be(409);
        }
    }

    [Fact]
    public async Task Post_task_without_priority_defaults_to_medium()
    {
        var ct = TestContext.Current.CancellationToken;
        await _f.ResetAsync();

        var title = $"no-priority-{Guid.NewGuid():N}";
        var res = await _f.Client.PostAsJsonAsync("/tasks", new { title }, _json, ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var id = await res.Content.ReadFromJsonAsync<Guid>(_json, ct);
        var task = (await GetTasksAsync()).Single(t => t.TaskId == id);
        task.Priority.Should().Be(Priority.Medium);
    }

    [Fact]
    public async Task Post_task_with_explicit_priority_keeps_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await _f.ResetAsync();

        var title = $"explicit-{Guid.NewGuid():N}";
        var res = await _f.Client.PostAsJsonAsync("/tasks",
            new { title, priority = Priority.High }, _json, ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var id = await res.Content.ReadFromJsonAsync<Guid>(_json, ct);
        var task = (await GetTasksAsync()).Single(t => t.TaskId == id);
        task.Priority.Should().Be(Priority.High);
    }

    [Fact]
    public async Task Put_complete_concurrent_calls_yield_one_success_and_rest_conflict()
    {
        await _f.ResetAsync();

        var id = await CreateTaskAsync($"race-{Guid.NewGuid():N}");

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _f.Client.PutAsync($"/tasks/{id}/complete", content: null))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        var statuses = responses.Select(r => r.StatusCode).ToList();

        statuses.Count(s => s == HttpStatusCode.OK).Should().Be(1);
        statuses.Count(s => s == HttpStatusCode.Conflict).Should().Be(4);
    }

    [Fact]
    public async Task Complete_publishes_task_completed_to_rabbitmq()
    {
        var ct = TestContext.Current.CancellationToken;

        await _f.ResetAsync();

        var title = $"publish-{Guid.NewGuid():N}";
        var id = await CreateTaskAsync(title);

        // Подписываемся ДО вызова complete, чтобы не пропустить сообщение
        await using var consumer = await TestRabbitConsumer.CreateAsync(
            _f.RabbitMq,
            exchange: "task.events",
            routingKey: "task.completed", ct);

        // Завершаем задачу
        var res = await _f.Client.PutAsync($"/tasks/{id}/complete", null, ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        // Ждём сообщение
        var msg = await consumer.WaitForMessageAsync(TimeSpan.FromSeconds(5));
        msg.Should().NotBeNull("TaskEventPublisher должен опубликовать task.completed");

        msg!.Value.RoutingKey.Should().Be("task.completed");

        var evt = JsonSerializer.Deserialize<TaskCompletedEvent>(msg.Value.Body, _json)!;
        evt.TaskId.Should().Be(id);
        evt.Title.Should().Be(title);
        evt.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        evt.Priority.Should().Be(Priority.High);
    }
}
