using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Moq;

namespace MinimalValidation.AspNetCore.Tests
{
    public class ValidatedOfT
    {
        [Theory]
        [InlineData("{\"Name\":\"Test Value\"}")]
        [InlineData("{\"name\":\"Test Value\"}")]
        public async Task BindAsync_Returns_Valid_Object_For_Valid_Json(string jsonBody)
        {
            var (httpContext, httpRequest, serviceProvider) = CreateMockHttpContext();
            var requestBody = Encoding.UTF8.GetBytes(jsonBody);
            httpRequest.SetupGet(x => x.ContentLength).Returns(requestBody.Length);
            httpRequest.SetupGet(x => x.Body).Returns(new MemoryStream(requestBody));
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.NotNull(result);
            if (result == null) throw new InvalidOperationException("Result should not be null here.");

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("{\"Name\":\"\"}", 1)]
        [InlineData("{\"WrongName\":\"A value\"}", 1)]
        [InlineData("{\"Name\":null}", 1)]
        [InlineData("{}", 1)]
        public async Task BindAsync_Returns_Invalid_Object_For_Invalid_Json(string jsonBody, int expectedErrorCount)
        {
            var (httpContext, httpRequest, serviceProvider) = CreateMockHttpContext();
            var requestBody = Encoding.UTF8.GetBytes(jsonBody);
            httpRequest.SetupGet(x => x.ContentLength).Returns(requestBody.Length);
            httpRequest.SetupGet(x => x.Body).Returns(new MemoryStream(requestBody));
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.NotNull(result);
            if (result == null) throw new InvalidOperationException("Result should not be null here.");

            Assert.False(result.IsValid);
            Assert.Equal(expectedErrorCount, result.Errors.Count);
        }

        [Fact]
        public async Task BindAsync_Returns_Null_For_Null_Request_Body()
        {
            var (httpContext, httpRequest, serviceProvider) = CreateMockHttpContext();
            var requestBody = Encoding.UTF8.GetBytes("null");
            httpRequest.SetupGet(x => x.ContentLength).Returns(requestBody.Length);
            httpRequest.SetupGet(x => x.Body).Returns(new MemoryStream(requestBody));
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.Null(result);
        }

        [Fact]
        public async Task BindAsync_Throws_BadRequestException_For_Non_Json_Request()
        {
            var (httpContext, httpRequest, serviceProvider) = CreateMockHttpContext();
            httpRequest.SetupGet(x => x.ContentType).Returns("text/plain");
            var parameterInfo = new Mock<ParameterInfo>();

            await Assert.ThrowsAsync<BadHttpRequestException>(async () =>
            {
                var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);
            });
        }

        [Fact]
        public async Task BindAsync_Uses_JsonOptions_From_DI()
        {
            var (httpContext, httpRequest, serviceProvider) = CreateMockHttpContext();
            var jsonOptions = new JsonOptions();
            var suffix = DateTime.UtcNow.Ticks.ToString();
            jsonOptions.SerializerOptions.Converters.Add(new AppendToStringJsonConverter(suffix));
            httpRequest.SetupGet(x => x.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes("{\"name\":\"test\"}")));
            serviceProvider.Setup(x => x.GetService(typeof(JsonOptions))).Returns(jsonOptions);
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.NotNull(result);
            if (result == null) throw new InvalidOperationException("Result should not be null here.");

            Assert.True(result.IsValid);
            Assert.Equal($"test{suffix}", result.Value.Name);
        }

        private (Mock<HttpContext>, Mock<HttpRequest>, Mock<IServiceProvider>) CreateMockHttpContext()
        {
            var httpContext = new Mock<HttpContext>();
            var httpRequest = new Mock<HttpRequest>();
            var serviceProvider = new Mock<IServiceProvider>();

            httpRequest.SetupGet(x => x.Method).Returns("GET");
            httpRequest.SetupGet(x => x.ContentType).Returns("application/json");
            httpRequest.SetupGet(x => x.HttpContext).Returns(httpContext.Object);
            httpContext.SetupGet(x => x.Request).Returns(httpRequest.Object);
            httpContext.SetupGet(x => x.RequestAborted).Returns(CancellationToken.None);
            httpContext.SetupGet(x => x.RequestServices).Returns(serviceProvider.Object);

            return (httpContext, httpRequest, serviceProvider);
        }

        private class TestType
        {
            [Required]
            public string? Name {  get; set; }
        }

        private class AppendToStringJsonConverter : JsonConverter<string>
        {
            private readonly string _suffix;

            public AppendToStringJsonConverter(string suffix)
            {
                _suffix = suffix;
            }

            public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.GetString() + _suffix;
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }
}