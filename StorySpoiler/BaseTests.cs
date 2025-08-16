using System;
using System.Net;
using NUnit.Framework;
using RestSharp;
using RestSharp.Authenticators;
using System.Text.Json;
using System.Collections.Generic;
using StorySpoiler.Models;

namespace StorySpoiler
{
    [TestFixture]
    public class StoryTests
    {
        private RestClient client = null!;
        private static string? createdStoryId;
        private const string BaseUrl = "https://d3s5nxhwblsjbi.cloudfront.net";

        [OneTimeSetUp]
        public void Setup()
        {
            string token = GetJwtToken("martin321", "123123");

            var options = new RestClientOptions(BaseUrl)
            {
                Authenticator = new JwtAuthenticator(token)
            };

            client = new RestClient(options);
            client.AddDefaultHeader("Accept", "application/json");
        }

        private string GetJwtToken(string username, string password)
        {
            var loginClient = new RestClient(BaseUrl);
            var request = new RestRequest("/api/User/Authentication", Method.Post)
                .AddJsonBody(new { UserName = username, Password = password });

            var response = loginClient.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Login failed – check API availability.");
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty, "Empty login response.");

            using var json = JsonDocument.Parse(response.Content!);

            string? token = null;
            if (json.RootElement.TryGetProperty("accessToken", out var at)) token = at.GetString();
            else if (json.RootElement.TryGetProperty("AccessToken", out at)) token = at.GetString();

            Assert.That(token, Is.Not.Null.And.Not.Empty, "No access token in login response.");
            return token!;
        }

        [Test, Order(1)]
        public void CreateStory_ShouldReturnCreated()
        {
            var body = new
            {
                Title = "QA Story",
                Description = "Created by automated test",
                Url = ""
            };

            var request = new RestRequest("/api/Story/Create", Method.Post)
                .AddJsonBody(body);

            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), response.Content);

            var json = JsonSerializer.Deserialize<JsonElement>(response.Content!);

            createdStoryId = json.GetProperty("storyId").GetString() ?? string.Empty;
            var msg = json.TryGetProperty("msg", out var m) ? m.GetString() : null;

            Assert.That(createdStoryId, Is.Not.Null.And.Not.Empty);
            Assert.That(msg, Does.Contain("Successfully created!"));
        }

        [Test, Order(2)]
        public void EditStory_ShouldReturnOk()
        {
            Assert.That(createdStoryId, Is.Not.Null.And.Not.Empty, "Create test must run first.");

            var body = new
            {
                Title = "Edited QA Story",
                Description = "Edited description from automated test",
                Url = ""
            };

            var request = new RestRequest($"/api/Story/Edit/{createdStoryId}", Method.Put)
                .AddJsonBody(body);

            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), response.Content);

            var json = JsonSerializer.Deserialize<JsonElement>(response.Content!);
            var msg = json.TryGetProperty("msg", out var m) ? m.GetString() : null;

            Assert.That(msg, Does.Contain("Successfully edited"));
        }

        [Test, Order(3)]
        public void GetAllStories_ShouldReturnList()
        {
            var request = new RestRequest("/api/Story/All", Method.Get);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), response.Content);

            var stories = JsonSerializer.Deserialize<List<object>>(response.Content!);

            Assert.That(stories, Is.Not.Empty, "Expected at least one story in the list.");
        }

        [Test, Order(4)]
        public void DeleteStory_ShouldReturnOk()
        {
            Assert.That(createdStoryId, Is.Not.Null.And.Not.Empty, "Create test must run first.");

            var request = new RestRequest($"/api/Story/Delete/{createdStoryId}", Method.Delete);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), response.Content);

            var json = JsonSerializer.Deserialize<JsonElement>(response.Content!);
            var msg = json.TryGetProperty("msg", out var m) ? m.GetString() : null;

            Assert.That(msg, Does.Contain("Deleted successfully!"));
        }

        [Test, Order(5)]
        public void CreateStory_WithoutRequiredFields_ShouldReturnBadRequest()
        {
            var body = new
            {
                Title = "",
                Description = "",
                Url = ""
            };

            var request = new RestRequest("/api/Story/Create", Method.Post)
                .AddJsonBody(body);

            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), response.Content);
        }

        [Test, Order(6)]
        public void Edit_NonExistingStory_ShouldReturnNotFound()
        {
            var nonExistingId = "999999999"; 

            var body = new
            {
                Title = "Ghost Story",
                Description = "This story should not exist",
                Url = ""
            };

            var request = new RestRequest($"/api/Story/Edit/{nonExistingId}", Method.Put)
                .AddJsonBody(body);

            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), response.Content);

            var json = JsonSerializer.Deserialize<JsonElement>(response.Content!);
            var msg = json.TryGetProperty("msg", out var m) ? m.GetString() : null;

            Assert.That(msg, Does.Contain("No spoilers"));
        }

        [Test, Order(7)]
        public void Delete_NonExistingStory_ShouldReturnBadRequest()
        {
            var nonExistingId = "999999998";

            var request = new RestRequest($"/api/Story/Delete/{nonExistingId}", Method.Delete);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), response.Content);

            var json = JsonSerializer.Deserialize<JsonElement>(response.Content!);
            var msg = json.TryGetProperty("msg", out var m) ? m.GetString() : null;

            Assert.That(msg, Does.Contain("Unable to delete this story spoiler!"));
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            client?.Dispose();
        }
    }
}
