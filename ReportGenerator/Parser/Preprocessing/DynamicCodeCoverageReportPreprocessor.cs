﻿using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Palmmedia.ReportGenerator.Parser.Preprocessing
{
    /// <summary>
    /// Preprocessor for reports generated by CodeCoverage.exe.
    /// </summary>
    internal class DynamicCodeCoverageReportPreprocessor
    {
        /// <summary>
        /// The report file as XContainer.
        /// </summary>
        private readonly XContainer report;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicCodeCoverageReportPreprocessor"/> class.
        /// </summary>
        /// <param name="report">The report.</param>
        internal DynamicCodeCoverageReportPreprocessor(XContainer report)
        {
            this.report = report;
        }

        /// <summary>
        /// Executes the preprocessing of the report.
        /// </summary>
        internal void Execute()
        {
            foreach (var module in this.report.Descendants("module").ToArray())
            {
                ApplyClassNameToStartupCodeElements(module);
            }
        }

        /// <summary>
        /// Applies the class name of the parent class to startup code elements.
        /// </summary>
        /// <param name="module">The module.</param>
        private static void ApplyClassNameToStartupCodeElements(XElement module)
        {
            var startupCodeFunctions = module
                .Elements("functions")
                .Elements("function")
                .Where(c => c.Attribute("type_name").Value.StartsWith("$", System.StringComparison.OrdinalIgnoreCase)
                    && c.Attribute("name").Value.StartsWith("Invoke(", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var functionsInModule = module
                .Elements("functions")
                .Elements("function")
                .Where(c => !c.Attribute("type_name").Value.StartsWith("$", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var startupCodeFunction in startupCodeFunctions)
            {
                var fileIds = startupCodeFunction
                    .Elements("ranges")
                    .Elements("range")
                    .Select(e => e.Attribute("source_id").Value)
                    .Distinct()
                    .ToArray();

                if (fileIds.Length != 1)
                {
                    continue;
                }

                var lineNumbers = startupCodeFunction
                    .Elements("ranges")
                    .Elements("range")
                    .Select(r => int.Parse(r.Attribute("start_line").Value, CultureInfo.InvariantCulture))
                    .OrderBy(v => v)
                    .Take(1)
                    .ToArray();

                if (lineNumbers.Length != 1)
                {
                    continue;
                }

                XElement closestClass = null;
                int closestLineNumber = 0;

                foreach (var function in functionsInModule)
                {
                    var linesOfClass = function
                        .Elements("ranges")
                        .Elements("range")
                        .ToArray();

                    var fileIdsOfClass = linesOfClass
                        .Select(e => e.Attribute("source_id").Value)
                        .Distinct()
                        .ToArray();

                    if (fileIdsOfClass.Length != 1 || fileIdsOfClass[0] != fileIds[0])
                    {
                        continue;
                    }

                    var lineNumbersOfClass = linesOfClass
                        .Select(r => int.Parse(r.Attribute("start_line").Value, CultureInfo.InvariantCulture))
                        .OrderBy(v => v)
                        .Take(1)
                        .ToArray();

                    /* Conditions:
                        * 1) No line numbers available
                        * 2) Class comes after current class
                        * 3) Closer class has already been found */
                    if (lineNumbersOfClass.Length != 1
                        || lineNumbersOfClass[0] > lineNumbers[0] 
                        || closestLineNumber > lineNumbersOfClass[0])
                    {
                        continue;
                    }
                    else
                    {
                        closestClass = function;
                        closestLineNumber = lineNumbersOfClass[0];
                    }
                }

                if (closestClass != null)
                {
                    startupCodeFunction.Attribute("type_name").Value = closestClass.Attribute("type_name").Value + "." + startupCodeFunction.Attribute("type_name").Value;
                }
            }
        }
    }
}
