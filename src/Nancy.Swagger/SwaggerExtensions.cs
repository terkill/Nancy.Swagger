﻿using Nancy.Routing;
using Swagger.ObjectModel;
using Swagger.ObjectModel.ApiDeclaration;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nancy.Swagger
{
    [SwaggerApi]
    public static class SwaggerExtensions
    {
        /// <summary>
        /// Returns an instance of <see cref="SwaggerRouteData"/> representing this route.
        /// </summary>
        /// <param name="desc">The <see cref="RouteDescription"/>.</param>
        /// <param name="action">An <see cref="Action{SwaggerRouteDataBuilder}"/> for building the <see cref="SwaggerRouteData"/>.</param>
        /// <returns>An instance of <see cref="SwaggerRouteData"/> constructed using <paramref name="desc"/> and by invoking <paramref name="action"/>.</returns>
        public static SwaggerRouteData AsSwagger(this RouteDescription desc, Action<SwaggerRouteDataBuilder> action)
        {
            var builder = new SwaggerRouteDataBuilder(desc.Name, Convert(desc.Method), desc.Path);

            action.Invoke(builder);

            return builder.Data;
        }

        public static IEnumerable<Type> GetDistinctModelTypes(this IList<SwaggerRouteData> routeData)
        {
            return GetOperationModels(routeData)
                .Union(GetParameterModels(routeData))
                .Select(GetType)
                .Where(type => !Primitive.IsPrimitive(type))
                .Distinct();
        }

        private static Type GetType(Type type)
        {
            if (type.IsContainer())
            {
                return type.GetElementType() ?? type.GetGenericArguments().First();
            }

            return type;
        }

        private static IEnumerable<Type> GetOperationModels(IEnumerable<SwaggerRouteData> metadata)
        {
            return metadata
                .Where(d => d.OperationModel != null)
                .Select(d => d.OperationModel);
        }

        private static IEnumerable<Type> GetParameterModels(IEnumerable<SwaggerRouteData> metadata)
        {
            return metadata
                .SelectMany(d => d.OperationParameters)
                .Where(p => p.ParameterModel != null)
                .Select(p => p.ParameterModel);
        }

        public static Operation ToOperation(this SwaggerRouteData routeData)
        {
            var operation = routeData.OperationModel.ToDataType<Operation>();

            operation.Nickname = SwaggerConfig.NicknameConvention(routeData);
            operation.Summary = routeData.OperationSummary;
            operation.Method = routeData.OperationMethod;
            operation.Notes = routeData.OperationNotes;
            operation.Parameters = routeData.OperationParameters.Select(p => p.ToParameter());
            operation.ResponseMessages = routeData.OperationResponseMessages.Any() ? routeData.OperationResponseMessages.OrderBy(r => r.Code) : null;
            operation.Produces = routeData.OperationProduces.Any() ? routeData.OperationProduces.OrderBy(p => p) : null;
            operation.Consumes = routeData.OperationConsumes.Any() ? routeData.OperationConsumes.OrderBy(c => c) : null;

            return operation;
        }

        public static T ToDataType<T>(this Type type, bool isTopLevel = false)
            where T : DataType, new()
        {
            var dataType = new T();

            if (type == null) 
            {
                dataType.Type = "void";

                return dataType;
            }

            if (Primitive.IsPrimitive(type))
            {
                var primitive = Primitive.FromType(type);

                dataType.Format = primitive.Format;
                dataType.Type = primitive.Type;

                return dataType;
            }

            if (type.IsContainer())
            {
                dataType.Type = "array";

                var itemsType = type.GetElementType() ?? type.GetGenericArguments().FirstOrDefault();
                
                if (Primitive.IsPrimitive(itemsType))
                {
                    var primitive = Primitive.FromType(itemsType);

                    dataType.Items = new Items
                    {
                        Type = primitive.Type,
                        Format = primitive.Format
                    };

                    return dataType;
                }

                dataType.Items = new Items { Ref = SwaggerConfig.ModelIdConvention(itemsType) };

                return dataType;
            }

            if (isTopLevel)
            {
                dataType.Ref = SwaggerConfig.ModelIdConvention(type);
                return dataType;
            }

            dataType.Type = SwaggerConfig.ModelIdConvention(type);

            return dataType;
        }

        public static Parameter ToParameter(this SwaggerParameterData parameterData)
        {
            var parameter = parameterData.ParameterModel.ToDataType<Parameter>();

            parameter.Name = parameterData.Name;
            parameter.ParamType = parameterData.ParamType;
            parameter.Description = parameterData.Description;
            parameter.DefaultValue = parameterData.DefaultValue;

            var paramType = parameter.ParamType;

            // 5.2.4 Parameter Object: If paramType is "path" then this field MUST be included and have the value true.
            if (paramType == ParameterType.Path)
            {
                parameter.Required = true;
            }
            else
            {
                parameter.Required = parameterData.Required || parameterData.ParameterModel.IsImplicitlyRequired() ? true : (bool?)null;
            }

            // 5.2.4 Parameter Object: The field may be used only if paramType is "query", "header" or "path".
            if (paramType == ParameterType.Query || paramType == ParameterType.Header || paramType == ParameterType.Path)
            {
                parameter.AllowMultiple = parameterData.ParameterModel.IsContainer() ? true : (bool?)null;
            }

            // 5.2.4 Parameter Object: If paramType is "body", the name is used only for 
            // Swagger-UI and Swagger-Codegen. In this case, the name MUST be "body".  
            if (paramType == ParameterType.Body)
            {
                parameter.Name = "body";
            }

            // 5.2.4 Parameter Object: Type field MUST be used to link to other models.
            if (parameterData.ParameterModel.IsContainer())
            {
                parameter.Type = parameter.Items.Type;
                parameter.Format = parameter.Items.Format;
                parameter.Items = null;
            }
            else
            {
                parameter.Type = parameter.Type ?? parameter.Ref;
                parameter.Ref = null;
            }

            return parameter;
        }

        public static IEnumerable<Model> ToModel(this SwaggerModelData model, IEnumerable<SwaggerModelData> knownModels = null)
        {
            var classProperties = model.Properties.Where(x => !Primitive.IsPrimitive(x.Type) && !x.Type.GetTypeInfo().IsEnum && !x.Type.GetTypeInfo().IsGenericType);

            var modelsData = knownModels ?? Enumerable.Empty<SwaggerModelData>();

            foreach (var swaggerModelPropertyData in classProperties)
            {
                var properties = GetPropertiesFromType(swaggerModelPropertyData.Type);

                var modelDataForClassProperty =
                    modelsData.FirstOrDefault(x => x.ModelType == swaggerModelPropertyData.Type);

                var id = modelDataForClassProperty == null
                    ? swaggerModelPropertyData.Type.Name
                    : SwaggerConfig.ModelIdConvention(modelDataForClassProperty.ModelType);

                var description = modelDataForClassProperty == null
                    ? swaggerModelPropertyData.Description
                    : modelDataForClassProperty.Description;

                var required = modelDataForClassProperty == null
                    ? properties.Where(p => p.Required || p.Type.IsImplicitlyRequired())
                        .Select(p => p.Name)
                        .OrderBy(name => name)
                        .ToList()
                    : modelDataForClassProperty.Properties
                        .Where(p => p.Required || p.Type.IsImplicitlyRequired())
                        .Select(p => p.Name)
                        .OrderBy(name => name)
                        .ToList();

                var modelproperties = modelDataForClassProperty == null
                    ? properties.OrderBy(x => x.Name).ToDictionary(p => p.Name, ToModelProperty)
                    : modelDataForClassProperty.Properties.OrderBy(x => x.Name)
                        .ToDictionary(p => p.Name, ToModelProperty);

                yield return new Model
                {
                    Id = id,
                    Description = description,
                    Required = required,
                    Properties = modelproperties
                };
            }

            var topLevelModel = new Model
            {
                Id = SwaggerConfig.ModelIdConvention(model.ModelType),
                Description = model.Description,
                Required = model.Properties
                    .Where(p => p.Required || p.Type.IsImplicitlyRequired())
                    .Select(p => p.Name)
                    .OrderBy(name => name)
                    .ToList(),
                Properties = model.Properties
                    .OrderBy(p => p.Name)
                    .ToDictionary(p => p.Name, ToModelProperty)

                // TODO: SubTypes and Discriminator
            };

            yield return topLevelModel;
        }

        public static ModelProperty ToModelProperty(this SwaggerModelPropertyData modelPropertyData)
        {
            var propertyType = modelPropertyData.Type;

            var isClassProperty = !Primitive.IsPrimitive(propertyType);

            var modelProperty = modelPropertyData.Type.ToDataType<ModelProperty>(isClassProperty);
            
            modelProperty.DefaultValue = modelPropertyData.DefaultValue;
            modelProperty.Description = modelPropertyData.Description;
            modelProperty.Enum = modelPropertyData.Enum;
            modelProperty.Minimum = modelPropertyData.Minimum;
            modelProperty.Maximum = modelPropertyData.Maximum;

            if (modelPropertyData.Type.IsContainer())
            {
                modelProperty.UniqueItems = modelPropertyData.UniqueItems ? true : (bool?)null;
            }

            return modelProperty;
        }

        private static HttpMethod Convert(string method)
        {
            switch (method)
            {
                case "DELETE":
                    return HttpMethod.Delete;
                case "GET":
                    return HttpMethod.Get;
                case "OPTIONS":
                    return HttpMethod.Options;
                case "PATCH":
                    return HttpMethod.Patch;
                case "POST":
                    return HttpMethod.Post;
                case "PUT":
                    return HttpMethod.Put;
                case "HEAD":
                    return HttpMethod.Head;
                default:
                    throw new NotSupportedException(string.Format("HTTP method '{0}' is not supported.", method));
            }
        }

        private static IList<SwaggerModelPropertyData> GetPropertiesFromType(Type type)
        {
            return type.GetProperties()
                .Select(property => new SwaggerModelPropertyData
                {
                    Name = property.Name,
                    Type = property.PropertyType
                }).ToList();
        }

        public static bool IsContainer(this Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type)
                && !typeof(string).IsAssignableFrom(type);
        }

        public static string ToCamelCase(this string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                return val;
            }

            var sb = new StringBuilder();
            var nextToUpper = true;
            foreach (var c in val.Trim())
            {
                if (char.IsLetter(c))
                {
                    if (sb.Length == 0)
                    {
                        sb.Append(char.ToLower(c));
                    }
                    else
                    {
                        sb.Append(nextToUpper ? char.ToUpper(c) : c);
                    }
                    nextToUpper = false;
                }
                else
                {
                    if (char.IsDigit(c))
                    {
                        if (sb.Length == 0)
                        {
                            sb.Append("_");
                        }
                        sb.Append(nextToUpper ? char.ToUpper(c) : c);
                    }
                    nextToUpper = true;
                }
            }

            return sb.ToString();
        }
        
        internal static bool IsImplicitlyRequired(this Type type)
        {
            return type.GetTypeInfo().IsValueType && !IsNullable(type);
        }

        internal static bool IsNullable(Type type)
        {
            return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}