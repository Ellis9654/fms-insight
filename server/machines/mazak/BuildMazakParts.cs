/* Copyright (c) 2018, John Lenz

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of John Lenz, Black Maple Software, SeedTactics,
      nor the names of other contributors may be used to endorse or
      promote products derived from this software without specific
      prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Linq;
using System.Collections.Generic;
using BlackMaple.MachineWatchInterface;

namespace MazakMachineInterface
{
  //A mazak part cooresponds to a (Job,PathGroup) pair.
  public class MazakPart
  {
    private static Serilog.ILogger Log = Serilog.Log.ForContext<MazakPart>();

    public readonly JobPlan Job;
    public readonly int DownloadID;
    public readonly List<MazakProcess> Processes;

    public MazakPart(JobPlan j, int downID, int partIdx)
    {
      Job = j;
      DownloadID = downID;
      Processes = new List<MazakProcess>();
      PartName = Job.PartName + ":" + DownloadID.ToString() + ":" + partIdx.ToString();
    }

    public MazakPartRow ToDatabaseRow()
    {
      var newPartRow = new MazakPartRow();
      newPartRow.Command = MazakWriteCommand.Add;
      newPartRow.PartName = PartName;
      newPartRow.Comment = Comment;
      newPartRow.Price = 0;
      newPartRow.MaterialName = "";
      newPartRow.TotalProcess = Processes.Count;
      return newPartRow;
    }

    public string PartName { get; }

    public string Comment
    {
      get
      {
        return CreateComment(Job.UniqueStr, Processes.Select(p => p.Path), Job.ManuallyCreatedJob);
      }
    }

    public static string CreateComment(string unique, IEnumerable<int> paths, bool manual)
    {
      if (manual)
        return unique + "-Path" + string.Join("-", paths) + "-1";
      else
        return unique + "-Path" + string.Join("-", paths) + "-0";
    }

    public static bool IsSailPart(string partName, string comment)
    {
      if (partName == null || comment == null || !comment.Contains("-Path")) return false;

      //sail parts are those with a colon and a positive integer after the colon
      string[] sSplit = partName.Split(':');
      if (sSplit.Length >= 2)
      {
        int v;
        if (int.TryParse(sSplit[1], out v) && v >= 0)
        {
          return true;
        }
      }

      return false;
    }

    public static string ExtractPartNameFromMazakPartName(string mazakPartName)
    {
      int loc = mazakPartName.IndexOf(':');
      if (loc >= 0) mazakPartName = mazakPartName.Substring(0, loc);
      return mazakPartName;
    }

    public static int ParseUID(string str)
    {
      string[] sSplit = str.Split(':');
      int v;
      if (sSplit.Length >= 2 && int.TryParse(sSplit[1], out v))
      {
        return v;
      }
      else
      {
        return -1;
      }
    }

    public interface IProcToPath
    {
      int PathForProc(int proc);
    }
    private class ProcToPath : IProcToPath
    {
      private string unique;
      private IReadOnlyList<int> paths;
      public ProcToPath(string u, IReadOnlyList<int> p)
      {
        unique = u;
        paths = p;
      }
      public int PathForProc(int proc)
      {
        if (proc >= 1 && proc <= paths.Count)
          return paths[proc - 1];
        else
        {
          Log.Debug("Unable to find path for {uniq} proc {proc}", unique, proc);
          return 1;
        }
      }
    }

    public static void ParseComment(string comment, out string unique, out IProcToPath procToPath, out bool manual)
    {
      int idx = comment.LastIndexOf("-Path");
      int lastDash = comment.LastIndexOf("-");

      if (lastDash < 0 || idx == lastDash || lastDash == comment.Length - 1)
      {
        //this is an old schedule without the entry
        manual = false;
      }
      else
      {
        if (comment[lastDash + 1] == '1')
          manual = true;
        else
          manual = false;
        comment = comment.Substring(0, lastDash);
      }

      if (idx >= 0)
      {
        unique = comment.Substring(0, idx);
        string[] pathsStr = comment.Substring(idx + 5).Split('-');
        procToPath = new ProcToPath(unique, pathsStr.Select(p =>
          int.TryParse(p, out int pNum) ? pNum : 1
        ).ToList());
      }
      else
      {
        unique = comment;
        procToPath = new ProcToPath(unique, new[] { 1 });
      }
    }
  }

  //There are two kinds of MazakProcesses
  //  - Processes which are copied from template rows (this is the old way, from
  //    2005 until early 2012)
  //  - Once path groups were added (2012), the processes can be created from the job.  All installs
  //    should use this from now on, but we support the old method for now until all the existing installs
  //    are converted over.

  public abstract class MazakProcess
  {
    public static string CreateMainProgramComment(string program, long revision)
    {
      return "Insight:" + revision.ToString() + ":" + program;
    }
    public static bool IsInsightMainProgram(string mainProgramComment)
    {
      return mainProgramComment.StartsWith("Insight:");
    }
    public static bool TryParseMainProgramComment(string mainProgramComment, out string program, out long rev)
    {
      if (mainProgramComment.StartsWith("Insight:"))
      {
        mainProgramComment = mainProgramComment.Substring(8);
        var idx = mainProgramComment.IndexOf(':');
        if (idx > 0)
        {
          if (long.TryParse(mainProgramComment.Substring(0, idx), out rev))
          {
            program = mainProgramComment.Substring(idx + 1);
            return true;
          }
        }
      }

      program = null;
      rev = 0;
      return false;
    }


    public readonly MazakPart Part;
    public readonly int ProcessNumber;
    public readonly int Path;

    public JobPlan Job
    {
      get { return Part.Job; }
    }

    protected MazakProcess(MazakPart parent, int proc, int path)
    {
      Part = parent;
      ProcessNumber = proc;
      Path = path;
    }

    public abstract IEnumerable<string> Pallets();
    public abstract (string fixture, int face) FixtureFace();
    public abstract BlackMaple.MachineWatchInterface.ProgramRevision PartProgram { get; }

    public abstract void CreateDatabaseRow(MazakPartRow newPart, string fixture, MazakDbType mazakTy);

    protected static int ConvertStatStrV1ToV2(string v1str)
    {
      int ret = 0;
      for (int i = 0; i <= v1str.Length - 1; i++)
      {
        if (v1str[i] != '0')
        {
          ret += Convert.ToInt32(Math.Pow(2, i));
        }
      }
      return ret;
    }
  }

  public class MazakProcessFromJob : MazakProcess
  {
    public override ProgramRevision PartProgram { get; }

    public MazakProcessFromJob(MazakPart parent, int process, int pth, ProgramRevision prog)
      : base(parent, process, pth)
    {
      PartProgram = prog;
    }

    public override IEnumerable<string> Pallets()
    {
      return Job.PlannedPallets(ProcessNumber, Path) ?? Enumerable.Empty<string>();
    }

    public override (string fixture, int face) FixtureFace()
    {
      return Job.PlannedFixture(ProcessNumber, Path);
    }

    public override void CreateDatabaseRow(MazakPartRow newPart, string fixture, MazakDbType mazakTy)
    {
      var newPartProcRow = new MazakPartProcessRow();
      newPartProcRow.PartName = Part.PartName;
      newPartProcRow.ProcessNumber = ProcessNumber;
      newPartProcRow.Fixture = fixture;
      newPartProcRow.FixQuantity = Math.Max(1, Job.PartsPerPallet(ProcessNumber, Path));

      newPartProcRow.ContinueCut = 0;
      //newPartProcRow.FixPhoto = "";
      //newPartProcRow.RemovePhoto = "";
      newPartProcRow.WashType = 0;

      char[] FixLDS = { '0', '0', '0', '0', '0', '0', '0', '0', '0', '0' };
      char[] UnfixLDS = { '0', '0', '0', '0', '0', '0', '0', '0', '0', '0' };
      char[] Cut = { '0', '0', '0', '0', '0', '0', '0', '0' };

      foreach (var routeEntry in Job.GetMachiningStop(ProcessNumber, Path))
      {
        foreach (int statNum in routeEntry.Stations)
        {
          Cut[statNum - 1] = statNum.ToString()[0];
        }
      }

      foreach (int statNum in Job.LoadStations(ProcessNumber, Path))
        FixLDS[statNum - 1] = statNum.ToString()[0];

      foreach (int statNum in Job.UnloadStations(ProcessNumber, Path))
        UnfixLDS[statNum - 1] = statNum.ToString()[0];

      newPartProcRow.MainProgram = PartProgram.CellControllerProgramName;
      newPartProcRow.FixLDS = new string(FixLDS);
      newPartProcRow.RemoveLDS = new string(UnfixLDS);
      newPartProcRow.CutMc = new string(Cut);

      if (mazakTy != MazakDbType.MazakVersionE)
      {
        newPartProcRow.FixLDS = ConvertStatStrV1ToV2(newPartProcRow.FixLDS).ToString();
        newPartProcRow.RemoveLDS = ConvertStatStrV1ToV2(newPartProcRow.RemoveLDS).ToString();
        newPartProcRow.CutMc = ConvertStatStrV1ToV2(newPartProcRow.CutMc).ToString();
      }

      newPart.Processes.Add(newPartProcRow);
    }

    public override string ToString()
    {
      return "CopyFromJob " + base.Job.PartName + "-" + base.ProcessNumber.ToString() +
        " path " + Path.ToString();
    }
  }

  public class MazakProcessFromTemplate : MazakProcess
  {
    public override ProgramRevision PartProgram { get; }

    //Here, jobs have only one process.  The number of processes are copied from the template.
    public readonly MazakPartProcessRow TemplateProcessRow;

    public MazakProcessFromTemplate(MazakPart parent, MazakPartProcessRow template, int path)
      : base(parent, template.ProcessNumber, path)
    {
      TemplateProcessRow = template;
      PartProgram = new ProgramRevision()
      {
        CellControllerProgramName = template.MainProgram
      };
    }

    public override IEnumerable<string> Pallets()
    {
      return Job.PlannedPallets(1, Path);
    }

    public override (string fixture, int face) FixtureFace() => (null, 0);

    public override void CreateDatabaseRow(MazakPartRow newPart, string fixture, MazakDbType mazakTy)
    {
      char[] FixLDS = { '0', '0', '0', '0', '0', '0', '0', '0', '0', '0' };
      char[] UnfixLDS = { '0', '0', '0', '0', '0', '0', '0', '0', '0', '0' };
      char[] Cut = { '0', '0', '0', '0', '0', '0', '0', '0' };

      foreach (var routeEntry in Job.GetMachiningStop(1, Path))
      {
        foreach (int statNum in routeEntry.Stations)
        {
          Cut[statNum - 1] = statNum.ToString()[0];
        }
      }

      foreach (int statNum in Job.LoadStations(1, Path))
      {
        FixLDS[statNum - 1] = statNum.ToString()[0];
      }

      foreach (int statNum in Job.UnloadStations(1, Path))
      {
        UnfixLDS[statNum - 1] = statNum.ToString()[0];
      }

      var newPartProcRow = TemplateProcessRow.Clone();
      newPartProcRow.PartName = Part.PartName;
      newPartProcRow.Fixture = fixture;
      newPartProcRow.ProcessNumber = ProcessNumber;

      newPartProcRow.FixLDS = new string(FixLDS);
      newPartProcRow.RemoveLDS = new string(UnfixLDS);
      newPartProcRow.CutMc = new string(Cut);

      if (mazakTy != MazakDbType.MazakVersionE)
      {
        newPartProcRow.FixLDS = ConvertStatStrV1ToV2(newPartProcRow.FixLDS).ToString();
        newPartProcRow.RemoveLDS = ConvertStatStrV1ToV2(newPartProcRow.RemoveLDS).ToString();
        newPartProcRow.CutMc = ConvertStatStrV1ToV2(newPartProcRow.CutMc).ToString();
      }

      newPart.Processes.Add(newPartProcRow);
    }

    public override string ToString()
    {
      return "CopyFromTemplate " + base.Job.PartName + "-" + base.ProcessNumber.ToString() +
        " path " + base.Path.ToString();
    }
  }

  //A group of parts and pallets where any part can run on any pallet.  In addition,
  //distinct processes can run at the same time on a pallet.
  public class MazakFixture
  {
    public int FixtureGroup { get; set; }
    public string JobFixtureName { get; set; }
    public string Face { get; set; }
    public List<MazakProcess> Processes = new List<MazakProcess>();
    public HashSet<string> Pallets = new HashSet<string>();
    public string MazakFixtureName { get; set; }

    // When a robot is configured in the Mazak software, it can jump ahead when searching
    // for the next part to load, which can cause parts to run out of sequence (because
    // the fixture group is used first to determine what to do next, then schedule due date,
    // then priority).  Setting all groups to zero is dangerous because it could place everything
    // simulatiously on the pallet, but at least the current version of Mazak won't place things
    // simulatiously on the pallet if the schedules have different due dates and priorities.
    // This hack of setting everything to zero is the only workaround on the current mazak version,
    // although that could change on any future versions.  Thus this must be manually configured
    // in a plugin after testing with the specific Mazak cell controller version in use.
    public static bool OverrideFixtureGroupToZero { get; set; } = false;

    public IEnumerable<MazakPalletRow> CreateDatabasePalletRows(MazakSchedulesPartsPallets oldData, int downloadUID)
    {
      var ret = new List<MazakPalletRow>();
      foreach (var pallet in Pallets.OrderBy(p => p))
      {
        int palNum = int.Parse(pallet);

        bool foundExisting = false;
        foreach (var palRow in oldData.Pallets)
        {
          if (palRow.PalletNumber == palNum && palRow.Fixture == MazakFixtureName)
          {
            foundExisting = true;
            break;
          }
        }
        if (foundExisting) continue;

        //Add rows to both V1 and V2.
        var newRow = new MazakPalletRow();
        newRow.Command = MazakWriteCommand.Add;
        newRow.PalletNumber = palNum;
        newRow.Fixture = MazakFixtureName;
        newRow.RecordID = 0;

        if (OverrideFixtureGroupToZero)
        {
          newRow.FixtureGroupV2 = 0;
          newRow.AngleV1 = 0;
        }
        else
        {
          newRow.FixtureGroupV2 = FixtureGroup;
          //combos with an angle in the range 0-999, and we don't want to conflict with that
          newRow.AngleV1 = (FixtureGroup * 1000);
        }

        ret.Add(newRow);
      }
      return ret;
    }
  }

  public class MazakJobs
  {
    public MazakAllData OldMazakData { get; set; }
    public MazakDbType MazakType { get; set; }
    public int DownloadUID { get; set; }
    public ISet<string> SavedParts { get; set; }

    //The representation of the mazak parts and pallets used to create the database rows
    public IEnumerable<MazakPart> AllParts { get; set; }
    public IEnumerable<MazakFixture> Fixtures { get; set; }

    //fixtures used by either existing or new parts.  Fixtures in the Mazak not in this list
    //will be deleted and fixtures appearing in this list but not yet in Mazak will be added.
    public ISet<string> UsedFixtures { get; set; }

    //main programs used by either existing or new parts
    public ISet<string> UsedMainProgramComments { get; set; }

    public MazakWriteData CreateDeleteFixtureAndProgramDatabaseRows(Func<string, long, string> getProgramContent, string programDir)
    {
      var ret = new MazakWriteData();

      //delete unused fixtures
      foreach (var fixRow in OldMazakData.Fixtures)
      {
        if (fixRow.Comment == "Insight")
        {
          if (!UsedFixtures.Contains(fixRow.FixtureName))
          {
            var newFixRow = fixRow.Clone();
            newFixRow.Command = MazakWriteCommand.Delete;
            ret.Fixtures.Add(newFixRow);
          }
        }
      }

      //add new fixtures
      foreach (string fixture in UsedFixtures)
      {
        //check if this fixture exists already... could exist already if we reuse fixtures
        foreach (var fixRow in OldMazakData.Fixtures)
        {
          if (fixRow.FixtureName == fixture)
          {
            goto found;
          }
        }

        var newFixRow = new MazakFixtureRow();
        newFixRow.Command = MazakWriteCommand.Add;
        newFixRow.FixtureName = fixture;
        newFixRow.Comment = "Insight";
        ret.Fixtures.Add(newFixRow);
      found:;
      }

      // delete old programs, but only if there is a newer revision for this program
      var maxRevForProg =
        OldMazakData.MainPrograms
          .Select(p => MazakProcess.TryParseMainProgramComment(p.Comment, out string pName, out long rev) ? new { pName, rev } : null)
          .Where(p => p != null)
          .ToLookup(p => p.pName, p => p.rev)
          .ToDictionary(ps => ps.Key, ps => ps.Max());

      foreach (var prog in OldMazakData.MainPrograms)
      {
        if (!MazakProcess.IsInsightMainProgram(prog.Comment)) continue;
        if (UsedMainProgramComments.Contains(prog.Comment)) continue;
        if (!MazakProcess.TryParseMainProgramComment(prog.Comment, out string pName, out long rev)) continue;
        if (rev >= maxRevForProg[pName]) continue;

        ret.Programs.Add(new NewMazakProgram()
        {
          Command = MazakWriteCommand.Delete,
          MainProgram = prog.MainProgram,
          Comment = prog.Comment
        });
      }

      // add programs
      var newProgs = new Dictionary<(string name, long rev), NewMazakProgram>();
      foreach (var part in AllParts)
      {
        foreach (var proc in part.Processes)
        {
          if (string.IsNullOrEmpty(proc.PartProgram.CellControllerProgramName) && !string.IsNullOrEmpty(proc.PartProgram.ProgramName))
          {
            NewMazakProgram newProg;
            if (newProgs.ContainsKey((proc.PartProgram.ProgramName, proc.PartProgram.Revision)))
            {
              newProg = newProgs[(proc.PartProgram.ProgramName, proc.PartProgram.Revision)];
            }
            else
            {
              newProg = new NewMazakProgram()
              {
                Command = MazakWriteCommand.Add,
                ProgramName = proc.PartProgram.ProgramName,
                ProgramRevision = proc.PartProgram.Revision,
                MainProgram = System.IO.Path.Combine(
                  programDir,
                  proc.PartProgram.ProgramName + "_rev" + proc.PartProgram.Revision.ToString() + ".EIA"
                ),
                Comment = MazakProcess.CreateMainProgramComment(proc.PartProgram.ProgramName, proc.PartProgram.Revision),
                ProgramContent = getProgramContent(proc.PartProgram.ProgramName, proc.PartProgram.Revision)
              };
              newProgs[(newProg.ProgramName, newProg.ProgramRevision)] = newProg;
            }
            proc.PartProgram.CellControllerProgramName = newProg.MainProgram;
          }
        }
      }
      foreach (var n in newProgs.Values)
        ret.Programs.Add(n);

      return ret;
    }

    public MazakWriteData CreatePartPalletDatabaseRows()
    {
      var ret = new MazakWriteData();
      foreach (var p in AllParts)
        ret.Parts.Add(p.ToDatabaseRow());

      var byName = ret.Parts.ToDictionary(p => p.PartName, p => p);

      foreach (var g in Fixtures)
      {
        foreach (var p in g.Processes)
        {
          p.CreateDatabaseRow(byName[p.Part.PartName], g.MazakFixtureName, MazakType);
        }

        foreach (var p in g.CreateDatabasePalletRows(OldMazakData, DownloadUID))
          ret.Pallets.Add(p);
      }

      return ret;
    }

    public MazakWriteData DeleteOldPartPalletRows()
    {
      var transSet = new MazakWriteData();
      foreach (var partRow in OldMazakData.Parts)
      {
        if (MazakPart.IsSailPart(partRow.PartName, partRow.Comment))
        {
          if (!SavedParts.Contains(partRow.PartName))
          {
            var newPartRow = partRow.Clone();
            newPartRow.Command = MazakWriteCommand.Delete;
            newPartRow.TotalProcess = newPartRow.Processes.Count();
            newPartRow.Processes.Clear(); // when deleting, don't need to add process rows
            transSet.Parts.Add(newPartRow);
          }
        }
      }

      foreach (var palRow in OldMazakData.Pallets)
      {
        int idx = palRow.Fixture.IndexOf(':');

        if (idx >= 0)
        {
          //check if this fixture is being used by a new schedule
          //or is a fixture used by a part in savedParts

          if (!UsedFixtures.Contains(palRow.Fixture))
          {
            //not found, can delete it
            var newPalRow = palRow.Clone();
            newPalRow.Command = MazakWriteCommand.Delete;
            transSet.Pallets.Add(newPalRow);
          }
        }
      }
      return transSet;
    }
  }

  public static class ConvertJobsToMazakParts
  {
    private static Serilog.ILogger Log = Serilog.Log.ForContext<MazakJobs>();

    //Allows plugins to customize the process creation
    public delegate MazakProcess ProcessFromJobDelegate(MazakPart parent, int process, int pth, BlackMaple.MachineWatchInterface.ProgramRevision prog);
    public static ProcessFromJobDelegate ProcessFromJob
      = (parent, process, pth, prog) => new MazakProcessFromJob(parent, process, pth, prog);

    public static MazakJobs JobsToMazak(
      IEnumerable<JobPlan> jobs,
      int downloadUID,
      MazakAllData mazakData,
      ISet<string> savedParts,
      MazakDbType MazakType,
      bool useStartingOffsetForDueDate,
      BlackMaple.MachineFramework.FMSSettings fmsSettings,
      Func<string, long?, BlackMaple.MachineWatchInterface.ProgramRevision> lookupProgram,
      IList<string> errors)
    {
      Validate(jobs, fmsSettings, errors);
      var allParts = BuildMazakParts(jobs, downloadUID, mazakData, MazakType, errors, lookupProgram);
      var usedMazakFixGroups = new HashSet<int>(mazakData.Pallets.Select(f => f.FixtureGroup));
      var groups = GroupProcessesIntoFixtures(allParts, usedMazakFixGroups, useStartingOffsetForDueDate, errors);

      var usedFixtures = AssignMazakFixtures(groups, downloadUID, mazakData, savedParts, useStartingOffsetForDueDate, errors);
      var usedProgs = CalculateUsedPrograms(mazakData, allParts);

      return new MazakJobs()
      {
        OldMazakData = mazakData,
        MazakType = MazakType,
        DownloadUID = downloadUID,
        SavedParts = savedParts,
        AllParts = allParts,
        Fixtures = groups,
        UsedFixtures = usedFixtures,
        UsedMainProgramComments = usedProgs
      };
    }

    #region Validate
    private static void Validate(
      IEnumerable<JobPlan> jobs, BlackMaple.MachineFramework.FMSSettings fmsSettings, IList<string> errs)
    {
      //only allow numeric pallets and check queues
      foreach (JobPlan part in jobs)
      {
        for (int proc = 1; proc <= part.NumProcesses; proc++)
        {
          for (int path = 1; path <= part.GetNumPaths(proc); path++)
          {
            foreach (string palName in part.PlannedPallets(proc, path))
            {
              int v;
              if (!int.TryParse(palName, out v))
              {
                errs.Add("Invalid pallet->part mapping. " + palName + " is not numeric.");
              }
            }

            var inQueue = part.GetInputQueue(proc, path);
            if (!string.IsNullOrEmpty(inQueue) && !fmsSettings.Queues.ContainsKey(inQueue))
            {
              errs.Add(
                " Job " + part.UniqueStr + " has an input queue " + inQueue + " which is not configured as a local queue in FMS Insight." +
                " All input queues must be local queues, not an external queue.");
            }

            var outQueue = part.GetOutputQueue(proc, path);
            if (proc == part.NumProcesses)
            {
              if (!string.IsNullOrEmpty(outQueue) && !fmsSettings.ExternalQueues.ContainsKey(outQueue))
              {
                errs.Add("Output queues on the final process must be external queues." +
                  " Job " + part.UniqueStr + " has a queue " + outQueue + " on the final process which is not configured " +
                  " as an external queue");
              }
            }
            else
            {
              if (!string.IsNullOrEmpty(outQueue) && !fmsSettings.Queues.ContainsKey(outQueue))
              {
                errs.Add(
                  " Job " + part.UniqueStr + " has an output queue " + outQueue + " which is not configured as a queue in FMS Insight." +
                  " Non-final processes must have a configured local queue, not an external queue");
              }
            }
          }
        }
      }
    }
    #endregion

    #region Parts
    private static List<MazakPart> BuildMazakParts(IEnumerable<JobPlan> jobs, int downloadID, MazakSchedulesPartsPallets mazakData, MazakDbType mazakTy,
                                                  IList<string> log,
                                                  Func<string, long?, ProgramRevision> lookupProgram)
    {
      var ret = new List<MazakPart>();
      int partIdx = 1;

      foreach (var job in jobs)
      {
        if (job.NumProcesses == 1)
        {

          //each path gets a MazakPart
          for (int path = 1; path <= job.GetNumPaths(1); path++)
          {
            var mazakPart = new MazakPart(job, downloadID, partIdx);
            partIdx += 1;

            string error;
            BuildProcFromJobWithOneProc(job, path, mazakPart, mazakTy, mazakData, lookupProgram, out error);
            if (error == null || error == "")
              ret.Add(mazakPart);
            else
              log.Add(error);
          }

        }
        else
        {

          //each path group gets a MazakPart

          //maps process 1 paths to groups
          var pathGroups = new Dictionary<int, int>();

          for (int path = 1; path <= job.GetNumPaths(1); path++)
          {
            var grp = job.GetPathGroup(1, path);

            if (pathGroups.ContainsValue(grp))
            {
              log.Add("Part " + job.PartName + " has a path group where process 1 " +
                "has more than one part in a group.  This configuration is not supported by" +
                " the Mazak cell controller.");
              goto skipPart;
            }

            pathGroups.Add(path, grp);
          }

          //check each path group has exactly one process for procs >= 2
          for (int proc = 2; proc <= job.NumProcesses; proc++)
          {
            foreach (int pGroup in pathGroups.Values)
            {
              int count = 0;
              for (int path = 1; path <= job.GetNumPaths(proc); path++)
              {
                if (job.GetPathGroup(proc, path) == pGroup)
                  count += 1;
              }

              if (count != 1)
              {
                log.Add("Part " + job.PartName + " has a part group where process " +
                        proc.ToString() + " has more than one path in the group.  This " +
                        "configuration is not supported by the Mazak cell controller. " +
                        "Skipping this part");
                goto skipPart;
              }
            }
          }

          foreach (var grp in pathGroups)
          {
            //label the part by the path number on process 1.
            var mazakPart = new MazakPart(job, downloadID, partIdx);
            partIdx += 1;
            string error;
            BuildProcFromPathGroup(job, mazakPart, out error, mazakTy, mazakData,
                 (proc, path) => grp.Value == job.GetPathGroup(proc, path), lookupProgram);

            if (error == null || error == "")
              ret.Add(mazakPart);
            else
              log.Add(error);
          }

        }

      skipPart:;
      }

      return ret;
    }

    //Func<int, int, bool> only introduced in .NET 3.5
    private delegate bool MatchFunc(int proc, int path);

    private static void BuildProcFromPathGroup(JobPlan job, MazakPart mazak, out string ErrorDuringCreate, MazakDbType mazakTy,
                                                   MazakSchedulesPartsPallets mazakData, MatchFunc matchPath,
                                                   Func<string, long?, ProgramRevision> lookupProgram)
    {
      ErrorDuringCreate = null;

      for (int proc = 1; proc <= job.NumProcesses; proc++)
      {
        for (int path = 1; path <= job.GetNumPaths(proc); path++)
        {
          if (matchPath(proc, path))
          {

            //Check this proc and path has a program
            bool has1Stop = false;
            ProgramRevision prog = null;
            foreach (var stop in job.GetMachiningStop(proc, path))
            {
              has1Stop = true;
              if (stop.Stations.Count == 0)
              {
                ErrorDuringCreate = "Part " + job.PartName + " has no stations assigned.";
                return;
              }

              if (stop.ProgramName == null || stop.ProgramName == "")
              {
                ErrorDuringCreate = "Part " + job.PartName + " has no programs.";
                return;
              }
              if (mazakTy == MazakDbType.MazakVersionE)
              {
                int progNum;
                if (!int.TryParse(stop.ProgramName, out progNum))
                {
                  ErrorDuringCreate = "Part " + job.PartName + " program " + stop.ProgramName +
                      " is not an integer.";
                  return;
                }
              }
              if (mazakData.MainPrograms.Any(mp => mp.MainProgram == stop.ProgramName))
              {
                prog = new ProgramRevision()
                {
                  CellControllerProgramName = stop.ProgramName
                };
              }
              else
              {
                prog = lookupProgram(stop.ProgramName, stop.ProgramRevision);
                if (prog == null)
                {
                  ErrorDuringCreate = "Part " + job.PartName + " program " + stop.ProgramName +
                    (stop.ProgramRevision.HasValue ? " rev" + stop.ProgramRevision.Value.ToString() : "") +
                    " does not exist in the cell controller.";
                  return;
                }
                // check if program already exists
                foreach (var mp in mazakData.MainPrograms)
                {
                  if (MazakProcess.TryParseMainProgramComment(mp.Comment, out string mpProg, out long mpRev))
                  {
                    if (mpProg == prog.ProgramName && mpRev == prog.Revision)
                    {
                      prog.CellControllerProgramName = mp.MainProgram;
                      break;
                    }
                  }
                }
              }
            }

            if (!has1Stop)
            {
              ErrorDuringCreate = "Part " + job.PartName + " has no machines assigned";
              return;
            }

            mazak.Processes.Add(ProcessFromJob(mazak, proc, path, prog));
          }
        }
      }
    }

    private static void BuildProcFromJobWithOneProc(JobPlan job, int proc1path, MazakPart mazak, MazakDbType mazakTy,
                                                    MazakSchedulesPartsPallets mazakData,
                                                    Func<string, long?, ProgramRevision> lookupProgram,
                                                    out string ErrorDuringCreate)
    {
      ErrorDuringCreate = null;

      // first try building from the job
      string FromJobError;
      BuildProcFromPathGroup(job, mazak, out FromJobError, mazakTy, mazakData,
            (proc, path) => path == proc1path, lookupProgram);  // proc will always equal 1.

      if (FromJobError == null || FromJobError == "")
        return; //Success

      mazak.Processes.Clear();

      // No programs, so check for a template row
      MazakPartRow TemplateRow = null;

      foreach (var pRow in mazakData.Parts)
      {
        if (pRow.PartName == job.PartName)
        {
          TemplateRow = pRow;
          break;
        }
      }

      if (TemplateRow == null)
      {
        ErrorDuringCreate = FromJobError + "  Also, no template row for " + job.PartName + " was found.";
        return;
      }

      foreach (var pRow in TemplateRow.Processes)
      {
        mazak.Processes.Add(new MazakProcessFromTemplate(mazak, pRow, proc1path));
      }
    }

    private static ISet<string> CalculateUsedPrograms(MazakAllData oldData, IEnumerable<MazakPart> newParts)
    {
      var progsToComment =
        oldData.MainPrograms
          .Where(p => MazakProcess.IsInsightMainProgram(p.Comment))
          .ToDictionary(p => p.MainProgram, p => p.Comment);


      var used = new HashSet<string>();
      foreach (var part in oldData.Parts)
      {
        foreach (var proc in part.Processes)
        {
          if (!string.IsNullOrEmpty(proc.MainProgram) && progsToComment.ContainsKey(proc.MainProgram))
          {
            used.Add(progsToComment[proc.MainProgram]);
          }
        }
      }
      foreach (var part in newParts)
      {
        foreach (var proc in part.Processes)
        {
          if (!string.IsNullOrEmpty(proc.PartProgram.ProgramName))
          {
            used.Add(MazakProcess.CreateMainProgramComment(proc.PartProgram.ProgramName, proc.PartProgram.Revision));
          }
        }
      }

      return used;
    }
    #endregion

    #region Fixtures

    //group together the processes that use the same fixture
    private static List<MazakFixture> GroupProcessesIntoFixtures(IEnumerable<MazakPart> allParts, HashSet<int> usedFixGroups, bool useStartingOffsetForDueDate, IList<string> logMessages)
    {
      var fixtures = new List<MazakFixture>();

      var sortedProcs =
        allParts
        .SelectMany(p => p.Processes)
        .GroupBy(p => (uniq: p.Job.UniqueStr, pathGroup: p is MazakProcessFromTemplate ? 0 : p.Job.GetPathGroup(p.ProcessNumber, p.Path)))
        .OrderBy(pathGroup =>
        {
          var minProc = pathGroup.FirstOrDefault(p => p.ProcessNumber == 1);
          if (minProc != null)
          {
            var start = minProc.Job.GetSimulatedStartingTimeUTC(minProc.ProcessNumber, minProc.Path);
            return start == DateTime.MinValue ? DateTime.MaxValue : start;
          }
          else
          {
            return DateTime.MaxValue;
          }
        })
        .SelectMany(pathGroup => pathGroup);

      foreach (var proc in sortedProcs)
      {
        var (jobFixtureName, face) = proc.FixtureFace();
        var pallets = new HashSet<string>(proc.Pallets());
        if (face <= 0) face = proc.ProcessNumber;
        if (jobFixtureName == "") jobFixtureName = null;

        // search for previous fixture to reuse
        int? group = null;
        MazakFixture oldFixture = null;
        foreach (var fixture in fixtures.ReverseEnumerator())
        {
          if (useStartingOffsetForDueDate)
          {
            // Mazak uses a change in group number as the primary determinant of what to run next.
            // If a mazak schedule is currently running with group number X on a pallet, the next thing to
            // run on that pallet will first check everything with group number X.  (This overrides the
            // due date and priority.)  Thus, if we are using the due date and priority, we can't share
            // fixtures/groups unless there is a continuous range of identical pallet sets or disjoint
            // pallet sets.

            if (pallets.Any(fixture.Pallets.Contains))
            {
              // potentially shared pallets, break unless exactly equal
              if (!fixture.Pallets.SetEquals(pallets))
              {
                break;
              }
            }
            else
            {
              // disjoint pallet sets, continue to next fixture
              continue;
            }
          }
          else
          {
            // everything has identical due date and priority so just compare pallet sets for equality.
            if (!fixture.Pallets.SetEquals(pallets)) continue;
          }

          if (fixture.JobFixtureName != jobFixtureName) continue;

          group = fixture.FixtureGroup;
          if (fixture.Face == face.ToString())
          {
            oldFixture = fixture;
            break;
          }
        }


        if (oldFixture != null)
        {
          oldFixture.Processes.Add(proc);
        }
        else
        {
          if (!group.HasValue)
          {
            group = Enumerable.Range(1, 998).First(i => !usedFixGroups.Contains(i));
            usedFixGroups.Add(group.Value);
          }
          var fix = new MazakFixture()
          {
            FixtureGroup = group.Value,
            JobFixtureName = jobFixtureName,
            Face = face.ToString(),
            Processes = new List<MazakProcess> { proc },
            Pallets = pallets,
          };
          fixtures.Add(fix);
        }
      }

      return fixtures;
    }

    private static ISet<string> AssignMazakFixtures(
      IEnumerable<MazakFixture> allFixtures, int downloadUID, MazakSchedulesPartsPallets mazakData, ISet<string> savedParts, bool useStartingOffsetForDueDate,
      IList<string> log)
    {
      //First calculate the available fixtures
      var usedMazakFixtureNames = new HashSet<string>();
      foreach (var partProc in mazakData.Parts.SelectMany(p => p.Processes))
      {
        if (partProc.PartName.IndexOf(':') >= 0)
        {
          if (savedParts.Contains(partProc.PartName))
          {
            usedMazakFixtureNames.Add(partProc.Fixture);
          }
        }
      }
      Log.Debug("Available Fixtures: {@fixs}", usedMazakFixtureNames);

      //For each fixture, create (or reuse) a mazak fixture and add parts and pallets using this fixture.
      foreach (var fixture in allFixtures)
      {
        //check if we can reuse an existing fixture
        if (!useStartingOffsetForDueDate)
        {
          Log.Debug("Searching existing fixtures for {@fixture}", fixture);
          CheckExistingFixture(fixture, usedMazakFixtureNames, mazakData);
        }

        if (string.IsNullOrEmpty(fixture.MazakFixtureName))
        {
          //create a new fixture
          var mazakFixtureName =
            "F:" +
            downloadUID.ToString() + ":" +
            fixture.FixtureGroup.ToString() + ":" +
            (string.IsNullOrEmpty(fixture.JobFixtureName) ? "" : fixture.JobFixtureName + ":") +
            fixture.Face;
          if (mazakFixtureName.Length > 20)
          {
            // take out the fixture name
            mazakFixtureName =
              "F:" +
              downloadUID.ToString() + ":" +
              fixture.FixtureGroup.ToString() + ":" +
              fixture.Face;
          }
          if (mazakFixtureName.Length > 20)
          {
            throw new BlackMaple.MachineFramework.BadRequestException(
              "Fixture " + mazakFixtureName + " is too long to fit in the Mazak databases");
          }
          fixture.MazakFixtureName = mazakFixtureName;
          Log.Debug("Creating new fixture {mazakFixtureName} for {@fixture}", mazakFixtureName, fixture);
        }

        usedMazakFixtureNames.Add(fixture.MazakFixtureName);
      }

      return usedMazakFixtureNames;
    }

    private static void CheckExistingFixture(
      MazakFixture fixture,
      ISet<string> availableMazakFixtures,
      MazakSchedulesPartsPallets mazakData)
    {
      //we need a fixture that exactly matches this pallet list and has all the processes.
      //Also, the fixture must be contained in a saved part.

      foreach (string mazakFixtureName in availableMazakFixtures)
      {
        int idx = mazakFixtureName.LastIndexOf(':');
        if (idx < 0) continue; //skip, not one of our fixtures

        //try to parse face
        if (mazakFixtureName.Substring(idx + 1) != fixture.Face) continue;

        //check pallets match
        var onPallets = new HashSet<string>();
        foreach (var palRow in mazakData.Pallets)
        {
          if (palRow.Fixture == mazakFixtureName)
          {
            onPallets.Add(palRow.PalletNumber.ToString());
          }
        }

        if (!onPallets.SetEquals(fixture.Pallets)) continue;

        fixture.MazakFixtureName = mazakFixtureName;
        Log.Debug("Assigning existing fixture {fix} to group {@group}", mazakFixtureName, mazakFixtureName);
        return;
      }
    }

    public static IEnumerable<T> ReverseEnumerator<T>(this IList<T> items)
    {
      for (int i = items.Count - 1; i >= 0; i--)
      {
        yield return items[i];
      }
    }
    #endregion
  }
}

