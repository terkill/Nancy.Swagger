using ApprovalTests;
using ApprovalTests.Reporters;
using Nancy.Swagger.Modules;
using Nancy.Swagger.Services;
using Nancy.Testing;
using Xunit;

namespace Nancy.Swagger.Annotations.Tests
{
    [UseReporter(typeof(XUnitReporter))]
    public class SwaggerAnnotationsProviderTests
    {
        private readonly Browser _browser;

        public SwaggerAnnotationsProviderTests()
        {
            var bootstrapper = new ConfigurableBootstrapper(with =>
            {
                with.ApplicationStartup((container, pipelines) => {
                    container.Register<ISwaggerMetadataProvider, SwaggerAnnotationsProvider>();
                });

                with.Module<SwaggerModule>();
                with.Module<TestRoutesModule>();
            });

            _browser = new Browser(bootstrapper);
        }

        [Fact]
        public async void Get_ApiDocsPath_ReturnsApiDeclaration()
        {
            ApproveJsonResponse(await _browser.Get("/api-docs/api-docs"));
        }

        [Fact]
        public async void Get_ApiDocsRootpath_ReturnsResourceListing()
        {
            ApproveJsonResponse(await _browser.Get("/api-docs"));
        }

        [Fact]
        public async void Get_TestModulePath_ReturnsApiDeclaration()
        {
            ApproveJsonResponse(await _browser.Get("/api-docs/testroutes"));
        }

        private static void ApproveJsonResponse(BrowserResponse response)
        {
            Approvals.VerifyJson(response.Body.AsString());
        }
    }
}