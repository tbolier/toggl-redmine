﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using TogglRedmine.Clients;
using TogglRedmine.Repositories.Redmine;
using TogglRedmine.Repositories.Toggl;
using TogglRedmine.Extensions;
using TogglRedmine.Model.Redmine;
using TogglRedmine.Model.Toggl;
using System.Text.RegularExpressions;

namespace TogglRedmine
{
    public class App
    {
        private readonly ITogglClient _togglClient;
        private readonly IRedmineClient _redmineClient;
        private readonly ILogger<App> _logger;
        private readonly Dictionary<string,int> _projectNameMapping;

        public App(
            IRedmineClient redmineClient, 
            ITogglClient togglClient, 
            ILogger<App> logger,
            IOptions<Dictionary<string,int>> projectNameMappingOptions)
        {
            _togglClient = togglClient;
            _redmineClient = redmineClient;
            _logger = logger;
            _projectNameMapping = projectNameMappingOptions.Value;
        }

        public void Run()
        {
            using (var togglRepo = new ReportsRepository(_togglClient))
            using (var redmineRepo = new TimeEntriesRepository(_redmineClient))
            {
                int counter = 0;

                var reports = togglRepo.GetAll().GetAwaiter().GetResult();
                _logger.LogInformation($"Found {reports.Count} reports for today.");  
                
                foreach (var report in reports)
                {
                    var timeInHours = report.GetDurationInHours();
                    try
                    {
                        var issueId = ExtractIssueId(report);
                        _logger.LogInformation($"{counter++} Logging {timeInHours} hours on issue {issueId}...");   
                        // redmineRepo.Add(new TimeEntry()
                        //     {
                        //        Issue = new Issue { Id = issueId },
                        //        Activity = new Activity { Id = 9 },
                        //        Comments = $"toggl - {report.Description}",
                        //        SpentOn = report.Start.UtcDateTime,
                        //        Hours = timeInHours,
                        //     }).GetAwaiter().GetResult();                        
                    } 
                    catch (KeyNotFoundException ex)
                    {
                        _logger.LogInformation($"{counter++} Skipping {report.Id}: {ex.Message}");
                        continue;                       
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation($"Unexpected error on report {report.Id}: {ex.Message}"); 
                    }
                }
            }            
        }

        private int ExtractIssueId(DetailedReport report)
        {
            // look for integers in the description
            var integerMatches = Regex.Match(report.Description, @"\d+");

            if (integerMatches.Success)
            {
                return Convert.ToInt32(integerMatches.Groups[0].Value);
            }
            else
            {
                // no integer found in the description, determine issue to track based on project name
                if (_projectNameMapping.ContainsKey(report.Project))
                {
                    return _projectNameMapping[report.Project];
                }
            }

            throw new KeyNotFoundException("Unable to determine issue id");
        }
    }
}
