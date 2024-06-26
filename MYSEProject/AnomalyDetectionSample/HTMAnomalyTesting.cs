﻿using NeoCortexApi;
using System;
using System.Collections.Generic;
using AnomalyDetectionSample;
using System.IO;
using System.Text;

namespace AnomalyDetection
{
    /// <summary>
    /// This class is responsible for testing an HTM model.
    /// CSV files from both training(learning) and predicting folders will be used for training our HTM Model.
    /// </summary>
    public class HTMAnomalyTesting
    {
        private readonly string _trainingFolderPath;
        private readonly string _predictingFolderPath;
        private static double _totalAccuracy = 0.0;
        private static int _iterationCount = 0;
        private readonly double _tolerance = 0.1;

        /// <summary>
        /// Initializes a new instance of the HTMAnomalyExperiment class with default folder paths.
        /// </summary>
        /// <param name="trainingFolderPath">The path to the training folder containing CSV files.</param>
        /// <param name="predictingFolderPath">The path to the predicting folder containing CSV files.</param>
        public HTMAnomalyTesting(string trainingFolderPath = "train_data", string predictingFolderPath = "predict_data")
        {
            string projectBaseDirectory = Directory.GetParent(Directory.GetCurrentDirectory())!.Parent!.Parent!.FullName;
            _trainingFolderPath = Path.Combine(projectBaseDirectory, trainingFolderPath);
            _predictingFolderPath = Path.Combine(projectBaseDirectory, predictingFolderPath);
        }

        /// <summary>
        /// Executes the anomaly detection Project using the HTM model.
        /// </summary>
        public void RunDetecting()
        {
            HTMTraining htmModel = new HTMTraining();
            Predictor predictor;

            htmModel.RunHTMTraining(_trainingFolderPath, _predictingFolderPath, out predictor);

            Console.WriteLine();
            Console.WriteLine("Begins the anomaly detection Process...");
            Console.WriteLine();

            CSVReader_Folder testSequencesReader = new CSVReader_Folder(_predictingFolderPath);
            var inputSequences = testSequencesReader.ReadFolder();
            var trimmedInputSequences = CSVReader_Folder.TrimSequences(inputSequences);
            predictor.Reset();

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputFile = $"rawOutput_{timestamp}.txt";
            string projectBaseDirectory = Directory.GetParent(Directory.GetCurrentDirectory())!.Parent!.Parent!.FullName;
            string outputFolderPath = Path.Combine(projectBaseDirectory, "output");
            string outputFilePath = Path.Combine(outputFolderPath, outputFile);

            List<List<string>> experimentOutputList = new List<List<string>>();

            foreach (List<double> sequence in trimmedInputSequences)
            {
                double[] sequenceArray = sequence.ToArray();
                List<string> sequenceOutputLines = new List<string>();

                try
                {
                    sequenceOutputLines = DetectAnomaly(predictor, sequenceArray, _tolerance);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Exception caught: {ex.Message}");
                }

                experimentOutputList.Add(sequenceOutputLines);
            }

            StringBuilder stringBuilder = new StringBuilder();

            foreach (List<string> innerList in experimentOutputList)
            {
                foreach (string line in innerList)
                {
                    stringBuilder.AppendLine(line);
                }
            }

            File.WriteAllText(outputFilePath, stringBuilder.ToString());

            TextOutput.totalAvgAccuracy = _totalAccuracy / _iterationCount;

            Console.WriteLine("Experiment results have been written to the text file.");
            Console.WriteLine("Anomaly Detection Sample Experiment completed.");
        }

        private List<string> DetectAnomaly(Predictor predictor, double[] sequence, double tolerance = 0.1)
        {
            if (sequence.Length < 2)
            {
                throw new ArgumentException($"Sequence must contain at least two values. Actual count: {sequence.Length}. Sequence: [{string.Join(",", sequence)}]");
            }

            foreach (double value in sequence)
            {
                if (double.IsNaN(value))
                {
                    throw new ArgumentException($"Sequence contains non-numeric values. Sequence: [{string.Join(",", sequence)}]");
                }
            }

            List<string> resultOutputLines = new List<string>
            {
                "------------------------------",
                "",
                $"Testing the sequence for anomaly detection: {string.Join(", ", sequence)}.",
                ""
            };

            double currentAccuracy = 0.0;

            for (int i = 0; i < sequence.Length; i++)
            {
                var currentItem = sequence[i];
                var predictionResult = predictor.Predict(currentItem);

                resultOutputLines.Add($"Current element in the testing sequence: {currentItem}");

                if (predictionResult.Count > 0)
                {
                    var tokens = predictionResult.First().PredictedInput.Split('_');
                    var tokens2 = predictionResult.First().PredictedInput.Split('-');
                    var similarity = predictionResult.First().Similarity;

                    if (i < sequence.Length - 1)
                    {
                        int nextIndex = i + 1;
                        double nextItem = sequence[nextIndex];
                        double predictedNextItem = double.Parse(tokens2.Last());

                        var anomalyScore = Math.Abs(predictedNextItem - nextItem);
                        var deviation = anomalyScore / nextItem;

                        if (deviation <= tolerance)
                        {
                            resultOutputLines.Add($"No anomaly detected in the next element. HTM Engine found similarity: {similarity}%.");
                            currentAccuracy += similarity;
                        }
                        else
                        {
                            resultOutputLines.Add($"****Anomaly detected**** in the next element. HTM Engine predicted: {predictedNextItem} with similarity: {similarity}%, actual value: {nextItem}.");
                            i++;
                            resultOutputLines.Add("Skipping to the next element in the testing sequence due to detected anomaly.");
                            currentAccuracy += similarity;
                        }
                    }
                    else
                    {
                        resultOutputLines.Add("End of sequence. Further anomaly testing cannot be continued.");
                    }
                }
                else
                {
                    resultOutputLines.Add("Nothing predicted from HTM Engine. Anomaly detection failed.");
                }
            }

            var averageSequenceAccuracy = currentAccuracy / sequence.Length;

            resultOutputLines.Add("");
            resultOutputLines.Add($"Average accuracy for this sequence: {averageSequenceAccuracy}%.");
            resultOutputLines.Add("");
            resultOutputLines.Add("------------------------------");

            _totalAccuracy += averageSequenceAccuracy;
            _iterationCount++;

            ShowOutputOnConsole(predictor, sequence, tolerance);

            return resultOutputLines;
        }

        private void ShowOutputOnConsole(Predictor predictor, double[] sequence, double tolerance)
        {
            Console.WriteLine("------------------------------");
            Console.WriteLine();
            Console.WriteLine($"Testing the sequence for anomaly detection: {string.Join(", ", sequence)}.");

            bool startFromFirst = true;
            double firstItem = sequence[0];
            double secondItem = sequence[1];

            var secondItemRes = predictor.Predict(secondItem);

            Console.WriteLine($"First element in the testing sequence from input list: {firstItem}");

            if (secondItemRes.Count > 0)
            {
                var stokens = secondItemRes.First().PredictedInput.Split('_');
                var stokens2 = secondItemRes.First().PredictedInput.Split('-');
                var stokens3 = secondItemRes.First().Similarity;
                var stokens4 = stokens2.Reverse().ElementAt(2);
                double predictedFirstItem = double.Parse(stokens4);
                var firstAnomalyScore = Math.Abs(predictedFirstItem - firstItem);
                var firstDeviation = firstAnomalyScore / firstItem;

                if (firstDeviation <= tolerance)
                {
                    Console.WriteLine($"No anomaly detected in the first element. HTM Engine found similarity: {stokens3}%. Starting check from beginning of the list.");
                    startFromFirst = true;
                }
                else
                {
                    Console.WriteLine($"****Anomaly detected**** in the first element. HTM Engine predicted: {predictedFirstItem} with similarity: {stokens3}%, actual value: {firstItem}. Moving to the next element.");
                    startFromFirst = false;
                }
            }
            else
            {
                Console.WriteLine("Anomaly detection cannot be performed for the first element. Starting check from beginning of the list.");
                startFromFirst = true;
            }

            int checkCondition = startFromFirst ? 0 : 1;

            for (int i = checkCondition; i < sequence.Length; i++)
            {
                var currentItem = sequence[i];
                var res = predictor.Predict(currentItem);
                Console.WriteLine($"Current element in the testing sequence from input list: {currentItem}");

                if (res.Count > 0)
                {
                    var tokens = res.First().PredictedInput.Split('_');
                    var tokens2 = res.First().PredictedInput.Split('-');
                    var tokens3 = res.First().Similarity;

                    if (i < sequence.Length - 1)
                    {
                        int nextIndex = i + 1;
                        double nextItem = sequence[nextIndex];
                        double predictedNextItem = double.Parse(tokens2.Last());

                        var anomalyScore = Math.Abs(predictedNextItem - nextItem);
                        var deviation = anomalyScore / nextItem;

                        if (deviation <= tolerance)
                        {
                            Console.WriteLine($"No anomaly detected in the next element. HTM Engine found similarity: {tokens3}%.");
                        }
                        else
                        {
                            Console.WriteLine($"****Anomaly detected**** in the next element. HTM Engine predicted: {predictedNextItem} with similarity: {tokens3}%, actual value: {nextItem}.");
                            i++; // Skip to the next element for checking, as we cannot use anomalous element for prediction
                            Console.WriteLine("As anomaly was detected, skipping to the next element in our testing sequence.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("End of input list. Further anomaly testing cannot be continued.");
                    }
                }
                else
                {
                    Console.WriteLine("Nothing predicted from HTM Engine. Anomaly detection failed.");
                }
            }
        }
    }
}
