using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;


namespace Unity.PackageManagement
{
    public static class NpmUtility
    {

      //  [MenuItem("test/npm")]
        public static void Test()
        {
            NpmOptions options = new NpmOptions();
            options.Registry = "http://npm.localhost.com:4873";
            options.Format = NpmResultFormat.Json;

            string result = Search("com.localhost", options);

        }

        public static string Search(string searchText, NpmOptions options = null)
        {
            List<string> args = new List<string>();
            args.Add("search");
            args.Add(searchText);
            options?.MakeArgs(args);
            return RunNpmCommand(args);
        }

        private static string RunNpmCommand(List<string> args)
        {
            var result = RunProcess("C:\\Program Files\\nodejs\\npm.cmd", args);
            //args.Insert(0, "npm");
            //var result = RunProcess("cmd", args);
            if (result.errorCode != 0)
                throw new Exception($"npm Command error\n" + result.result);
            return result.result;
        }


        private static (int errorCode, string result) RunProcess(string filePath, string argments = null, string workingDirectory = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = filePath;
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = Path.GetFullPath(workingDirectory);
            }
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.Arguments = argments;


            return RunProcess(startInfo);
        }

        private static (int errorCode, string result) RunProcess(string filePath, IEnumerable<string> argments = null, string workingDirectory = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = filePath;
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = Path.GetFullPath(workingDirectory);
            }
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            if (argments != null)
            {
                foreach (var arg in argments)
                {
                    //startInfo.ArgumentList.Add(arg);

                }
                startInfo.Arguments = string.Join(" ", argments.Select(o => $"\"{o}\""));
            }

            return RunProcess(startInfo);
        }


        private static (int errorCode, string result) RunProcess(ProcessStartInfo startInfo)
        {
            //StringBuilder output = new();
            //StringBuilder error = null;
            string output = string.Empty;
            string error = string.Empty;
            try
            {
                using (var proc = new Process())
                {

                    startInfo.RedirectStandardOutput = true;
                    //startInfo.RedirectStandardInput = true;
                    //startInfo.StandardOutputEncoding = Encoding.UTF8;
                    startInfo.RedirectStandardError = true;
                    //startInfo.StandardErrorEncoding = Encoding.UTF8;

                    proc.StartInfo = startInfo;
                    //proc.OutputDataReceived += (sender, e) =>
                    //{
                    //    if (output == null)
                    //        output = new StringBuilder();
                    //    output.AppendLine(e.Data);
                    //};
                    //proc.ErrorDataReceived += (sender, e) =>
                    //{
                    //    if (e.Data == null)
                    //        return;
                    //    if (error == null)
                    //        error = new StringBuilder();
                    //    error.AppendLine(e.Data);
                    //};
                    //proc.EnableRaisingEvents = true;
                    proc.Start();
                    //proc.BeginErrorReadLine();
                    //proc.BeginOutputReadLine();
                    output = proc.StandardOutput.ReadToEnd();
                    error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                    {
                        return (proc.ExitCode, error?.ToString());
                    }

                    if (error != null)
                        return (-1, error.ToString());

                    //result = proc.StandardOutput.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return (-1, ex.ToString());
            }
            return (0, output.ToString());
        }
    }

    public enum NpmResultFormat
    {
        Text,
        Json
    }

    public class NpmOptions
    {
        public string Registry { get; set; }

        public NpmResultFormat Format { get; set; } = NpmResultFormat.Text;

        internal void MakeArgs(List<string> args)
        {
            if (Format == NpmResultFormat.Json)
            {
                //args.Add("--json");
            }

            if (!string.IsNullOrEmpty(Registry))
            {
                args.Add("--registry");
                args.Add(Registry);
            }
        }
    }

}
