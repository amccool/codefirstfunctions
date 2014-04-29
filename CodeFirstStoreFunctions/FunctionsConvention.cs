﻿// Copyright (c) Pawel Kadluczka, Inc. All rights reserved. See License.txt in the project root for license information.

namespace CodeFirstStoreFunctions
{
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Data.Entity.Core.Mapping;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.Infrastructure;
    using System.Data.Entity.ModelConfiguration.Conventions;
    using System.Diagnostics;
    using System.Linq;

    public class FunctionsConvention<T> : IStoreModelConvention<EntityContainer>
        where T : DbContext
    {
        private readonly string _defaultSchema;

        public FunctionsConvention(string defaultSchema)
        {
            _defaultSchema = defaultSchema;
        }

        public void Apply(EntityContainer item, DbModel model)
        {
            var functionImports = new FunctionDiscovery(model, typeof (T)).FindFunctionImports();
            var storeFunctionBuilder = new StoreFunctionBuilder(model, _defaultSchema);

            foreach (var functionImport in functionImports)
            {
                var functionImportDefinition = CreateFunctionImport(model, functionImport);
                var storeFunctionDefinition = storeFunctionBuilder.Create(functionImport);
                model.ConceptualModel.Container.AddFunctionImport(functionImportDefinition);
                model.StoreModel.AddItem(storeFunctionDefinition);

                if (functionImportDefinition.IsComposableAttribute)
                {
                    model.ConceptualToStoreMapping.AddFunctionImportMapping(
                        new FunctionImportMappingComposable(
                            functionImportDefinition,
                            storeFunctionDefinition,
                            new FunctionImportResultMapping(),
                            model.ConceptualToStoreMapping));
                }
                else
                {
                    model.ConceptualToStoreMapping.AddFunctionImportMapping(
                        new FunctionImportMappingNonComposable(
                            functionImportDefinition,
                            storeFunctionDefinition,
                            new FunctionImportResultMapping[0],
                            model.ConceptualToStoreMapping));
                }
            }

            // TODO: scalar functions?, model defined functions?, multiple result sets?
        }

        private static EdmFunction CreateFunctionImport(DbModel model, FunctionImport functionImport)
        {
            EntitySet[] entitySets;
            FunctionParameter[] returnParameters;
            CreateReturnParameters(model, functionImport, out returnParameters, out entitySets);

            var functionPayload =
                new EdmFunctionPayload
                {
                    Parameters =
                        functionImport
                            .Parameters
                            .Select(p => FunctionParameter.Create(p.Key, p.Value, ParameterMode.In))
                            .ToList(),
                    ReturnParameters = returnParameters,
                    IsComposable = functionImport.IsComposable,
                    IsFunctionImport = true,
                    EntitySets = entitySets
                };

            return EdmFunction.Create(
                functionImport.Name,
                model.ConceptualModel.Container.Name,
                DataSpace.CSpace,
                functionPayload,
                null);
        }

        private static void CreateReturnParameters(DbModel model, FunctionImport functionImport, 
            out FunctionParameter[] returnParameters, out EntitySet[] entitySets)
        {
            var resultCount = functionImport.ReturnTypes.Count();
            entitySets = new EntitySet[resultCount];
            returnParameters = new FunctionParameter[resultCount];

            for (var i = 0; i < resultCount; i++)
            {
                var returnType = functionImport.ReturnTypes[i];

                if (returnType.BuiltInTypeKind == BuiltInTypeKind.EntityType)
                {
                    // TODO: derived types?
                    entitySets[i] = model.ConceptualModel.Container.EntitySets.Single(s => s.ElementType == returnType);
                }

                returnParameters[i] = FunctionParameter.Create(
                    "ReturnParam" + i,
                    returnType.GetCollectionType(),
                    ParameterMode.ReturnValue);
            }            
        }
    }
}