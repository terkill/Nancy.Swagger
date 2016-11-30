using Nancy.ModelBinding;
using Nancy.Swagger.Demo.Models;

namespace Nancy.Swagger.Demo.Modules
{
    public class HomeModule : NancyModule
    {
        public HomeModule()
        {
            Get("/", _ => "Hello Swagger! Visit http://localhost:3999/docs/index.html and enter http://localhost:3999/api-docs in the Swagger UI", null, "Home");

            Get("/users", _ => new[] { new User { Name = "Vincent Vega", Age = 45 } }, null, "GetUsers");

            Post("/users", _ =>
            {
                var result = this.BindAndValidate<User>();

                if (!ModelValidationResult.IsValid)
                {
                    return Negotiate.WithModel(new { Message = "Oops" })
                        .WithStatusCode(HttpStatusCode.UnprocessableEntity);
                }

                return Negotiate.WithModel(result).WithStatusCode(HttpStatusCode.Created);
            }, null, "PostUsers");
        }
    }
}
