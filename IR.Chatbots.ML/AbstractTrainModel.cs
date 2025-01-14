﻿using log4net;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
using IR.Chatbots.Common.Logging;
using IR.Chatbots.ML.Interfaces;
using System;
using System.Collections.Generic;

namespace IR.Chatbots.ML
{
    /// <summary>
    /// Abstract implementation of ML train model.
    /// </summary>
    /// <typeparam name="Input"></typeparam>
    /// <typeparam name="Output"></typeparam>
    public abstract class AbstractTrainModel<Input, Output> : ITrainModel<Input, Output>
        where Input : class, IMlData
        where Output : class, IMlData
    {
        protected static ILog logger = LogHelper.GetLogger<AbstractTrainModel<Input, Output>>();

        protected long _dataCount = 0;
        protected MLContext _mlContext = new MLContext();
        protected IDataView _trainingDataView;
        protected ITransformer _trainedModel;

        protected bool EnableCacheCheckpoint() => _dataCount < 10000;
        public abstract string ModelOutputPath { get; }

        private void Save(string filePath)
        {
            _mlContext.Model.Save(_trainedModel, _trainingDataView.Schema, filePath);   //Save
        }

        private void BuildAndTrainModel()
        {
            var trainingPipeline = TransformAndBuildPipeline();
            _trainedModel = trainingPipeline.Fit(_trainingDataView);    //Train
        }

        public void BuildAndSaveModel(string outputPath = null)
        {
            try
            {
                var trainData = LoadData();
                _dataCount = trainData.Count;
                _trainingDataView = _mlContext.Data.LoadFromEnumerable<Input>(trainData);
                BuildAndTrainModel();
                Save(outputPath ?? ModelOutputPath);
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }

        public abstract List<Input> LoadData();
        public abstract EstimatorChain<KeyToValueMappingTransformer> TransformAndBuildPipeline();

    }
}
