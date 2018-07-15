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
using System.Collections.Generic;
using System.Data;

namespace MazakMachineInterface
{
  public enum MazakDbType
  {
    MazakVersionE,
    MazakWeb,
    MazakSmooth
  }

  public interface IWriteData
  {
    MazakDbType MazakType {get;}
    void ClearTransactionDatabase();
    void SaveTransaction(TransactionDataSet dset, System.Collections.Generic.IList<string> log, string prefix, int checkInterval = -1);
  }

  public class MazakPartRow
  {
    public int Id {get;set;}
    public string Comment {get;set;}
    public string PartName {get;set;}
    public int? Price {get;set;}

    public IList<MazakPartProcessRow> Processes {get;} = new List<MazakPartProcessRow>();
  }

  public class MazakPartProcessRow
  {
    public int MazakPartRowId {get;set;}
    public MazakPartRow MazakPartRow {get;set;}

    public string PartName {get;set;}
    public int ProcessNumber {get;set;}

    public int FixQuantity {get;set;}
    public int? ContinueCut {get;set;}
    public string CutMc {get;set;}
    public string FixLDS {get;set;}
    public string FixPhoto {get;set;}
    public string Fixture {get;set;}
    public string MainProgram {get;set;}
    public string RemoveLDS {get;set;}
    public string RemovePhoto {get;set;}
    public int? WashType {get;set;}
  }

  public class MazakScheduleRow
  {
    public int Id {get;set;}
    public string Comment {get;set;}
    public string PartName {get;set;}
    public int PlanQuantity {get;set;}
    public int CompleteQuantity {get;set;}
    public int Priority {get;set;}

    public DateTime? DueDate {get;set;}
    public int? FixForMachine {get;set;}
    public int? HoldMode {get;set;}
    public int? MissingFixture {get;set;}
    public int? MissingProgram {get;set;}
    public int? MissingTool {get;set;}
    public int? MixScheduleID {get;set;}
    public int? ProcessingPriority {get;set;}
    public int? Reserved {get;set;}
    public int? UpdatedFlag {get;set;}

    public IList<MazakScheduleProcessRow> Processes {get;} = new List<MazakScheduleProcessRow>();
  }

  public class MazakScheduleProcessRow
  {
    public int MazakScheduleRowId {get;set;}
    public MazakScheduleRow MazakScheduleRow {get;set;}
    public int FixQuantity {get;set;}

    public int ProcessNumber {get;set;}
    public int ProcessMaterialQuantity {get;set;}
    public int ProcessExecuteQuantity {get;set;}
    public int ProcessBadQuantity {get;set;}
    public int ProcessMachine {get;set;}
    public int UpdatedFlag {get;set;}
  }
  public class MazakPalletRow
  {
    public int PalletNumber {get;set;}
    public string Fixture {get;set;}
    public int RecordID {get;set;}
    public int AngleV1 {get;set;}
    public int FixtureGroupV2 {get;set;}
  }

  public class MazakPalletSubStatusRow
  {
    public int PalletNumber {get;set;}
    public string FixtureName {get;set;}
    public int ScheduleID {get;set;}
    public string PartName {get;set;}
    public int PartProcessNumber {get;set;}
    public int FixQuantity {get;set;}
  }

  public class MazakPalletPositionRow
  {
    public int PalletNumber {get;set;}
    public string PalletPosition {get;set;}
  }

  public class MazakFixtureRow
  {
    public string FixtureName {get;set;}
    public string Comment {get;set;}
  }

  public class MazakSchedules
  {
    public IEnumerable<MazakScheduleRow> Schedules {get;set;}

    public void FindSchedule(string mazakPartName, int proc, out string unique, out int path, out int numProc)
    {
      unique = "";
      numProc = proc;
      path = 1;
      foreach (var schRow in Schedules)
      {
        if (schRow.PartName == mazakPartName && !string.IsNullOrEmpty(schRow.Comment))
        {
          bool manual;
          MazakPart.ParseComment(schRow.Comment, out unique, out var procToPath, out manual);
          numProc = schRow.Processes.Count;
          if (numProc < proc) numProc = proc;
          path = procToPath.PathForProc(proc);
          return;
        }
      }
    }
  }

  public class MazakSchedulesAndLoadActions : MazakSchedules
  {
    public IEnumerable<LoadAction> LoadActions {get;set;}
  }

  public class MazakSchedulesPartsPallets : MazakSchedules
  {
    public IEnumerable<MazakPartRow> Parts {get;set;}
    public IEnumerable<MazakPalletRow> Pallets {get;set;}
    public IEnumerable<MazakPalletSubStatusRow> PalletSubStatuses {get;set;}
    public IEnumerable<MazakPalletPositionRow> PalletPositions {get;set;}
    public IEnumerable<LoadAction> LoadActions {get;set;}
    public ISet<string> MainPrograms {get;set;}
  }

  public class MazakAllData : MazakSchedulesPartsPallets
  {
    public IEnumerable<MazakFixtureRow> Fixtures {get;set;}
  }

  public interface IReadDataAccess
  {
    MazakDbType MazakType {get;}
    MazakSchedules LoadSchedules();
    MazakSchedulesAndLoadActions LoadSchedulesAndLoadActions();
    MazakSchedulesPartsPallets LoadSchedulesPartsPallets();
    MazakAllData LoadAllData();

    TResult WithReadDBConnection<TResult>(Func<IDbConnection, TResult> action);
  }

}