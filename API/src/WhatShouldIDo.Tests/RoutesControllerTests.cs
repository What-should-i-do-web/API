using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WhatShouldIDo.Application.DTOs.Requests;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Tests.IntegrationTests
{/*
    public class RoutesControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public RoutesControllerTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Crud_Routes_Work_Correctly()
        {
            // CREATE
            var createReq = new CreateRouteRequest("Test Route");
            var createRes = await _client.PostAsJsonAsync("/api/routes", createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await createRes.Content.ReadFromJsonAsync<RouteDto>();
            created.Should().NotBeNull();

            // READ ALL
            var getAllRes = await _client.GetAsync("/api/routes");
            getAllRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // UPDATE
            var updateReq = new UpdateRouteRequest("Updated Route");
            var updateRes = await _client.PutAsJsonAsync($"/api/routes/{created!.Id}", updateReq);
            updateRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // DELETE
            var deleteRes = await _client.DeleteAsync($"/api/routes/{created.Id}");
            deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // VERIFY DELETION
            var getRes = await _client.GetAsync($"/api/routes/{created.Id}");
            getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
 
    }
    */
}
    