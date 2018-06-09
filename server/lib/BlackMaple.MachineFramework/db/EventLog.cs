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
using System.Data;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace BlackMaple.MachineFramework
{
    public class JobLogDB : MachineWatchInterface.ILogDatabase, MachineWatchInterface.IInspectionControl
    {

        #region Database Create/Update
        private SqliteConnection _connection;
        private object _lock;
        private Random _rand = new Random();

        public JobLogDB()
        {
            _lock = new object();
        }
        public JobLogDB(SqliteConnection c) : this()
        {
            _connection = c;
        }

        public void Open(string filename, string oldInspDbFile = null)
        {
            if (System.IO.File.Exists(filename))
            {
                _connection = SqliteExtensions.Connect(filename, newFile: false);
                _connection.Open();
                UpdateTables(oldInspDbFile);
            }
            else
            {
                _connection = SqliteExtensions.Connect(filename, newFile: true);
                _connection.Open();
                try
                {
                    CreateTables();
                }
                catch
                {
                    _connection.Close();
                    System.IO.File.Delete(filename);
                    throw;
                }
            }
        }

        public void Close()
        {
            _connection.Close();
        }


        private const int Version = 17;

        public void CreateTables()
        {
            var cmd = _connection.CreateCommand();

            cmd.CommandText = "CREATE TABLE version(ver INTEGER)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO version VALUES(" + Version.ToString() + ")";
            cmd.ExecuteNonQuery();

            //StationLoc, StationName and StationNum columns should be named
            //LogType, LocationName, and LocationNum but the column names are kept for backwards
            //compatibility
            cmd.CommandText = "CREATE TABLE stations(Counter INTEGER PRIMARY KEY AUTOINCREMENT,  Pallet TEXT," +
                 "StationLoc INTEGER, StationName TEXT, StationNum INTEGER, Program TEXT, Start INTEGER, TimeUTC INTEGER," +
                 "Result TEXT, EndOfRoute INTEGER, Elapsed INTEGER, ActiveTime INTEGER, ForeignID TEXT, OriginalMessage TEXT)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE material(Counter INTEGER, MaterialID INTEGER, UniqueStr TEXT," +
                 "Process INTEGER, Part TEXT, NumProcess INTEGER, Face TEXT, PRIMARY KEY(Counter,MaterialID,Process))";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX stations_idx ON stations(TimeUTC)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX stations_pal ON stations(Pallet, Result)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX stations_foreign ON stations(ForeignID)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX material_idx ON material(MaterialID)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE materialid(MaterialID INTEGER PRIMARY KEY AUTOINCREMENT," +
                 "UniqueStr TEXT NOT NULL, Serial TEXT, Workorder TEXT)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX materialid_serial ON materialid(Serial) WHERE Serial IS NOT NULL";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX materialid_workorder ON materialid(Workorder) WHERE Workorder IS NOT NULL";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX materialid_uniq ON materialid(UniqueStr)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE pendingloads(Pallet TEXT, Key TEXT, LoadStation INTEGER, Elapsed INTEGER, ActiveTime INTEGER, ForeignID TEXT)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX pending_pal on pendingloads(Pallet)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX pending_foreign on pendingloads(ForeignID)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE program_details(Counter INTEGER, Key TEXT, Value TEXT, " +
                "PRIMARY KEY(Counter, Key))";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE inspection_counters(Counter TEXT PRIMARY KEY, Val INTEGER, LastUTC INTEGER)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE inspection_next_piece(StatType INTEGER, StatNum INTEGER, InspType TEXT, PRIMARY KEY(StatType,StatNum, InspType))";
            cmd.ExecuteNonQuery();
        }


        private void UpdateTables(string inspDbFile)
        {
            var cmd = _connection.CreateCommand();

            cmd.CommandText = "SELECT ver FROM version";

            int curVersion = 0;

            try
            {
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        curVersion = (int)reader.GetInt32(0);
                    }
                    else
                    {
                        curVersion = 0;
                    }
                }

            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("no such table") >= 0)
                {
                    curVersion = 0;
                }
                else
                {
                    throw;
                }
            }

            if (curVersion > Version)
            {
                throw new Exception("This input file was created with a newer version of machine watch.  Please upgrade machine watch");
            }

            if (curVersion == Version) return;


            var trans = _connection.BeginTransaction();

            try
            {
                //add upgrade code here, in seperate functions
                if (curVersion < 1) Ver0ToVer1(trans);

                if (curVersion < 2) Ver1ToVer2(trans);

                if (curVersion < 3) Ver2ToVer3(trans);

                if (curVersion < 4) Ver3ToVer4(trans);

                if (curVersion < 5) Ver4ToVer5(trans);

                if (curVersion < 6) Ver5ToVer6(trans);

                if (curVersion < 7) Ver6ToVer7(trans);

                if (curVersion < 8) Ver7ToVer8(trans);

                if (curVersion < 9) Ver8ToVer9(trans);

                if (curVersion < 10) Ver9ToVer10(trans);

                if (curVersion < 11) Ver10ToVer11(trans);

                if (curVersion < 12) Ver11ToVer12(trans);

                if (curVersion < 13) Ver12ToVer13(trans);

                if (curVersion < 14) Ver13ToVer14(trans);

                if (curVersion < 15) Ver14ToVer15(trans);

                if (curVersion < 16) Ver15ToVer16(trans);

                if (curVersion < 17) Ver16ToVer17(trans, inspDbFile);

                //update the version in the database
                cmd.Transaction = trans;
                cmd.CommandText = "UPDATE version SET ver = " + Version.ToString();
                cmd.ExecuteNonQuery();

                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }

            //only vacuum if we did some updating
            cmd.Transaction = null;
            cmd.CommandText = "VACUUM";
            cmd.ExecuteNonQuery();
        }

        private void Ver0ToVer1(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE material ADD Part TEXT";
            cmd.ExecuteNonQuery();
        }

        private void Ver1ToVer2(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE material ADD NumProcess INTEGER";
            cmd.ExecuteNonQuery();
        }

        private void Ver2ToVer3(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE stations ADD EndOfRoute INTEGER";
            cmd.ExecuteNonQuery();
        }

        private void Ver3ToVer4(IDbTransaction trans)
        {
            // This version added columns which have since been removed.
        }

        private void Ver4ToVer5(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE stations ADD Elapsed INTEGER";
            cmd.ExecuteNonQuery();
        }

        private void Ver5ToVer6(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE material ADD Face TEXT";
            cmd.ExecuteNonQuery();
        }

        private void Ver6ToVer7(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE stations ADD ForeignID TEXT";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX stations_pal ON stations(Pallet, EndOfRoute);";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX stations_foreign ON stations(ForeignID)";
            cmd.ExecuteNonQuery();
        }

        private void Ver7ToVer8(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "CREATE TABLE pendingloads(Pallet TEXT, Key TEXT, LoadStation INTEGER, Elapsed INTEGER, ForeignID TEXT)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX pending_pal on pendingloads(Pallet)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX pending_foreign on pendingloads(ForeignID)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "DROP INDEX stations_pal";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX stations_pal ON stations(Pallet, Result)";
            cmd.ExecuteNonQuery();

        }

        private void Ver8ToVer9(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "CREATE TABLE program_details(Counter INTEGER, Key TEXT, Value TEXT, " +
                "PRIMARY KEY(Counter, Key))";
            cmd.ExecuteNonQuery();
        }

        private void Ver9ToVer10(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE stations ADD ActiveTime INTEGER";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "ALTER TABLE pendingloads ADD ActiveTime INTEGER";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "ALTER TABLE materialid ADD Serial TEXT";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX materialid_serial ON materialid(Serial) WHERE Serial IS NOT NULL";
            cmd.ExecuteNonQuery();
        }

        private void Ver10ToVer11(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE stations ADD OriginalMessage TEXT";
            cmd.ExecuteNonQuery();
        }

        private void Ver11ToVer12(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE materialid ADD Workorder TEXT";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX materialid_workorder ON materialid(Workorder) WHERE Workorder IS NOT NULL";
            cmd.ExecuteNonQuery();
        }

        private void Ver12ToVer13(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "ALTER TABLE stations ADD StationName TEXT";
            cmd.ExecuteNonQuery();
        }

        private void Ver13ToVer14(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "CREATE TABLE sersettings(ID INTEGER PRIMARY KEY, SerialType INTEGER, SerialLength INTEGER, DepositProc INTEGER, FilenameTemplate TEXT, ProgramTemplate TEXT)";
            cmd.ExecuteNonQuery();
        }

        private void Ver14ToVer15(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "CREATE INDEX materialid_uniq ON materialid(UniqueStr)";
            cmd.ExecuteNonQuery();
        }

        private void Ver15ToVer16(IDbTransaction trans)
        {
            IDbCommand cmd = _connection.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "DROP TABLE sersettings";
            cmd.ExecuteNonQuery();
        }

        private void Ver16ToVer17(IDbTransaction trans, string inspDbFile)
        {
            var cmd = _connection.CreateCommand();
            ((IDbCommand)cmd).Transaction = trans;

            cmd.CommandText = "CREATE TABLE inspection_counters(Counter TEXT PRIMARY KEY, Val INTEGER, LastUTC INTEGER)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE inspection_next_piece(StatType INTEGER, StatNum INTEGER, InspType TEXT, PRIMARY KEY(StatType,StatNum, InspType))";
            cmd.ExecuteNonQuery();

            if (string.IsNullOrEmpty(inspDbFile)) return;

            cmd.CommandText = "ATTACH DATABASE $db AS insp";
            cmd.Parameters.Add("db", SqliteType.Text).Value = inspDbFile;
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();

            cmd.CommandText = "INSERT INTO main.inspection_counters SELECT * FROM insp.counters";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO main.inspection_next_piece SELECT * FROM insp.next_piece";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "DETACH DATABASE insp";
            cmd.ExecuteNonQuery();
        }
        #endregion

        #region Event
        public event MachineWatchInterface.NewLogEntryDelegate NewLogEntry;
        #endregion

        #region Loading
        private List<MachineWatchInterface.LogEntry> LoadLog(IDataReader reader)
        {
            var matCmd = _connection.CreateCommand();
            matCmd.CommandText = "SELECT MaterialID, UniqueStr, Process, Part, NumProcess, Face FROM material WHERE Counter = $cntr ORDER BY Counter ASC";
            matCmd.Parameters.Add("cntr", SqliteType.Integer);

            var detailCmd = _connection.CreateCommand();
            detailCmd.CommandText = "SELECT Key, Value FROM program_details WHERE Counter = $cntr";
            detailCmd.Parameters.Add("cntr", SqliteType.Integer);

            var lst = new List<MachineWatchInterface.LogEntry>();

            while (reader.Read())
            {
                long ctr = reader.GetInt64(0);
                string pal = reader.GetString(1);
                int logType = reader.GetInt32(2);
                int locNum = reader.GetInt32(3);
                string prog = reader.GetString(4);
                bool start = reader.GetBoolean(5);
                System.DateTime timeUTC = new DateTime(reader.GetInt64(6), DateTimeKind.Utc);
                string result = reader.GetString(7);
                bool endOfRoute = false;
                if (!reader.IsDBNull(8))
                    endOfRoute = reader.GetBoolean(8);
                TimeSpan elapsed = TimeSpan.FromMinutes(-1);
                if (!reader.IsDBNull(9))
                    elapsed = TimeSpan.FromTicks(reader.GetInt64(9));
                TimeSpan active = TimeSpan.Zero;
                if (!reader.IsDBNull(10))
                    active = TimeSpan.FromTicks(reader.GetInt64(10));
                string locName = null;
                if (!reader.IsDBNull(11))
                    locName = reader.GetString(11);

                MachineWatchInterface.LogType ty;
                if (Enum.IsDefined(typeof(MachineWatchInterface.LogType), logType))
                {
                    ty = (MachineWatchInterface.LogType)logType;
                    if (locName == null)
                    {
                        //For compatibility with old logs
                        switch (ty)
                        {
                            case MachineWatchInterface.LogType.GeneralMessage:
                                locName = "General";
                                break;
                            case MachineWatchInterface.LogType.Inspection:
                                locName = "Inspect";
                                break;
                            case MachineWatchInterface.LogType.LoadUnloadCycle:
                                locName = "Load";
                                break;
                            case MachineWatchInterface.LogType.MachineCycle:
                                locName = "MC";
                                break;
                            case MachineWatchInterface.LogType.OrderAssignment:
                                locName = "Order";
                                break;
                            case MachineWatchInterface.LogType.PartMark:
                                locName = "Mark";
                                break;
                            case MachineWatchInterface.LogType.PalletCycle:
                                locName = "Pallet Cycle";
                                break;
                        }
                    }
                }
                else
                {
                    ty = MachineWatchInterface.LogType.GeneralMessage;
                    switch (logType)
                    {
                        case 3: locName = "Machine"; break;
                        case 4: locName = "Buffer"; break;
                        case 5: locName = "Cart"; break;
                        case 8: locName = "Wash"; break;
                        case 9: locName = "Deburr"; break;
                        default: locName = "Unknown"; break;
                    }
                }

                var matLst = new List<MachineWatchInterface.LogMaterial>();
                matCmd.Parameters[0].Value = ctr;
                using (var matReader = matCmd.ExecuteReader())
                {
                    while (matReader.Read())
                    {
                        string part = "";
                        int numProc = -1;
                        string face = "";
                        if (!matReader.IsDBNull(3))
                            part = matReader.GetString(3);
                        if (!matReader.IsDBNull(4))
                            numProc = matReader.GetInt32(4);
                        if (!matReader.IsDBNull(5))
                            face = matReader.GetString(5);
                        matLst.Add(new MachineWatchInterface.LogMaterial(matReader.GetInt64(0),
                                                                         matReader.GetString(1),
                                                                         matReader.GetInt32(2),
                                                                         part, numProc, face));
                    }
                }

                var logRow = new MachineWatchInterface.LogEntry(ctr, matLst, pal,
                      ty, locName, locNum,
                    prog, start, timeUTC, result, endOfRoute, elapsed, active);

                detailCmd.Parameters[0].Value = ctr;
                using (var detailReader = detailCmd.ExecuteReader())
                {
                    while (detailReader.Read())
                    {
                        logRow.ProgramDetails[detailReader.GetString(0)] = detailReader.GetString(1);
                    }
                }

                lst.Add(logRow);
            }

            return lst;
        }

        public List<MachineWatchInterface.LogEntry> GetLogEntries(System.DateTime startUTC, System.DateTime endUTC)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                     " FROM stations WHERE TimeUTC >= $start AND TimeUTC <= $end ORDER BY Counter ASC";

                cmd.Parameters.Add("start", SqliteType.Integer).Value = startUTC.Ticks;
                cmd.Parameters.Add("end", SqliteType.Integer).Value = endUTC.Ticks;

                using (var reader = cmd.ExecuteReader())
                {
                    return LoadLog(reader);
                }
            }
        }

        public List<MachineWatchInterface.LogEntry> GetLog(long counter)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                     " FROM stations WHERE Counter > $cntr ORDER BY Counter ASC";
                cmd.Parameters.Add("cntr", SqliteType.Integer).Value = counter;

                using (var reader = cmd.ExecuteReader())
                {
                    return LoadLog(reader);
                }
            }
        }

        public List<MachineWatchInterface.LogEntry> StationLogByForeignID(string foreignID)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                     " FROM stations WHERE ForeignID = $foreign ORDER BY Counter ASC";
                cmd.Parameters.Add("foreign", SqliteType.Text).Value = foreignID;

                using (var reader = cmd.ExecuteReader())
                {
                    return LoadLog(reader);
                }
            }
        }

        public string OriginalMessageByForeignID(string foreignID)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT OriginalMessage " +
                     " FROM stations WHERE ForeignID = $foreign ORDER BY Counter DESC LIMIT 1";
                cmd.Parameters.Add("foreign", SqliteType.Text).Value = foreignID;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.IsDBNull(0))
                        {
                            return "";
                        }
                        else
                        {
                            return reader.GetString(0);
                        }
                    }
                }
            }
            return "";
        }

        public List<MachineWatchInterface.LogEntry> GetLogForMaterial(long materialID)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                     " FROM stations WHERE Counter IN (SELECT Counter FROM material WHERE MaterialID = $mat) ORDER BY Counter ASC";
                cmd.Parameters.Add("mat", SqliteType.Integer).Value = materialID;

                using (var reader = cmd.ExecuteReader())
                {
                    return LoadLog(reader);
                }
            }
        }

        public List<MachineWatchInterface.LogEntry> GetLogForSerial(string serial)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                    " FROM stations WHERE Counter IN (SELECT material.Counter FROM materialid INNER JOIN material ON material.MaterialID = materialid.MaterialID WHERE materialid.Serial = $ser) ORDER BY Counter ASC";
                cmd.Parameters.Add("ser", SqliteType.Text).Value = serial;

                using (var reader = cmd.ExecuteReader())
                {
                    return LoadLog(reader);
                }
            }
        }

        public List<MachineWatchInterface.LogEntry> GetLogForJobUnique(string jobUnique)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                    " FROM stations WHERE Counter IN (SELECT material.Counter FROM materialid INNER JOIN material ON material.MaterialID = materialid.MaterialID WHERE materialid.UniqueStr = $uniq) ORDER BY Counter ASC";
                cmd.Parameters.Add("uniq", SqliteType.Text).Value = jobUnique;

                using (var reader = cmd.ExecuteReader())
                {
                    return LoadLog(reader);
                }
            }
        }

        public List<MachineWatchInterface.LogEntry> GetLogForWorkorder(string workorder)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                    " FROM stations WHERE Counter IN (SELECT material.Counter FROM materialid INNER JOIN material ON material.MaterialID = materialid.MaterialID WHERE materialid.Workorder = $work) ORDER BY Counter ASC";
                cmd.Parameters.Add("work", SqliteType.Text).Value = workorder;

                using (var reader = cmd.ExecuteReader())
                {
                    return LoadLog(reader);
                }
            }
        }

        public List<MachineWatchInterface.LogEntry> GetCompletedPartLogs(DateTime startUTC, DateTime endUTC)
        {
            var searchCompleted = @"
                SELECT Counter FROM material
                    WHERE MaterialId IN
                        (SELECT material.MaterialId FROM stations, material
                            WHERE
                                stations.Counter = material.Counter
                                AND
                                stations.EndOfRoute = 1
                                AND
                                stations.TimeUTC <= $endUTC
                                AND
                                stations.TimeUTC >= $startUTC
                                AND
                                material.Process = material.NumProcess
                        )";

            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                    " FROM stations WHERE Counter IN (" + searchCompleted + ") ORDER BY Counter ASC";
                cmd.Parameters.Add("endUTC", SqliteType.Integer).Value = endUTC.Ticks;
                cmd.Parameters.Add("startUTC", SqliteType.Integer).Value = startUTC.Ticks;

                using (var reader = cmd.ExecuteReader())
                {
                    return LoadLog(reader);
                }
            }
        }

        public DateTime LastPalletCycleTime(string pallet)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT TimeUTC FROM stations where Pallet = $pal AND Result = 'PalletCycle' " +
                                     "ORDER BY Counter DESC LIMIT 1";
                cmd.Parameters.Add("pal", SqliteType.Text).Value = pallet;

                var date = cmd.ExecuteScalar();
                if (date == null || date == DBNull.Value)
                    return DateTime.MinValue;
                else
                    return new DateTime((long)date, DateTimeKind.Utc);
            }
        }

        //Loads the log for the current pallet cycle, which is all events from the last Result = "PalletCycle"
        public List<MachineWatchInterface.LogEntry> CurrentPalletLog(string pallet)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT MAX(Counter) FROM stations where Pallet = $pal AND Result = 'PalletCycle'";
                cmd.Parameters.Add("pal", SqliteType.Text).Value = pallet;

                var counter = cmd.ExecuteScalar();

                if (counter == DBNull.Value)
                {

                    cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                        " FROM stations WHERE Pallet = $pal ORDER BY Counter ASC";
                    using (var reader = cmd.ExecuteReader())
                    {
                        return LoadLog(reader);
                    }

                }
                else
                {

                    cmd.CommandText = "SELECT Counter, Pallet, StationLoc, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, StationName " +
                        " FROM stations WHERE Pallet = $pal AND Counter > $cntr ORDER BY Counter ASC";
                    cmd.Parameters.Add("cntr", SqliteType.Integer).Value = (long)counter;

                    using (var reader = cmd.ExecuteReader())
                    {
                        return LoadLog(reader);
                    }
                }
            }
        }

        public System.DateTime MaxLogDate()
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT MAX(TimeUTC) FROM stations";

                System.DateTime ret = DateTime.MinValue;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            ret = new DateTime(reader.GetInt64(0), DateTimeKind.Utc);
                        }
                    }
                }

                return ret;
            }
        }

        public string MaxForeignID()
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();

                cmd.CommandText = "SELECT MAX(ForeignID) FROM stations";
                var maxStat = cmd.ExecuteScalar();

                cmd.CommandText = "SELECT MAX(ForeignID) FROM pendingloads";
                var maxLoad = cmd.ExecuteScalar();

                if (maxStat == DBNull.Value && maxLoad == DBNull.Value)
                    return "";
                else if (maxStat != DBNull.Value && maxLoad == DBNull.Value)
                    return (string)maxStat;
                else if (maxStat == DBNull.Value && maxLoad != DBNull.Value)
                    return (string)maxLoad;
                else
                {
                    var s = (string)maxStat;
                    var l = (string)maxLoad;
                    if (s.CompareTo(l) > 0)
                        return s;
                    else
                        return l;
                }
            }
        }

        public string ForeignIDForCounter(long counter)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT ForeignID FROM stations WHERE Counter = $cntr";
                cmd.Parameters.Add("cntr", SqliteType.Integer).Value = counter;
                var ret = cmd.ExecuteScalar();
                if (ret == DBNull.Value)
                    return "";
                else if (ret == null)
                    return "";
                else
                    return (string)ret;
            }
        }

        public bool CycleExists(MachineWatchInterface.LogEntry cycle)
        {
            lock (_lock)
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM stations WHERE " +
                        "TimeUTC = $time AND Pallet = $pal AND StationLoc = $loc AND StationNum = $locnum AND StationName = $locname";
                    cmd.Parameters.Add("time", SqliteType.Integer).Value = cycle.EndTimeUTC.Ticks;
                    cmd.Parameters.Add("pal", SqliteType.Text).Value = cycle.Pallet;
                    cmd.Parameters.Add("loc", SqliteType.Integer).Value = (int)cycle.LogType;
                    cmd.Parameters.Add("locnum", SqliteType.Integer).Value = cycle.LocationNum;
                    cmd.Parameters.Add("locname", SqliteType.Text).Value = cycle.LocationName;

                    var ret = cmd.ExecuteScalar();
                    if (ret == null || Convert.ToInt32(ret) <= 0)
                        return false;
                    else
                        return true;
                }
            }
        }

		public List<MachineWatchInterface.WorkorderSummary> GetWorkorderSummaries(IEnumerable<string> workorders)
		{
			var countQry = @"
				SELECT material.Part, COUNT(material.MaterialID) FROM stations, material, materialid
  					WHERE
   						stations.Counter = material.Counter
   						AND
   						stations.EndOfRoute = 1
   						AND
   						material.MaterialID = materialid.MaterialID
   						AND
   						materialid.Workorder = $workid
                        AND
                        material.Process == material.NumProcess
                    GROUP BY
                        material.Part";

			var serialQry = @"
				SELECT DISTINCT Serial FROM materialid
				    WHERE
					    materialid.Workorder = $workid";

            var finalizedQry = @"
                SELECT MAX(TimeUTC) FROM stations
                    WHERE
                        Pallet = ''
                        AND
                        Result = $workid
                        AND
                        StationLoc = $workloc"; //use the (Pallet, Result) index

            var timeQry = @"
                SELECT Part, StationName, SUM(Elapsed / totcount), SUM(ActiveTime / totcount)
                    FROM
                        (
                            SELECT s.StationName, m.Part, s.Elapsed, s.ActiveTime,
                                 (SELECT COUNT(*) FROM material AS m2 WHERE m2.Counter = s.Counter) totcount
                              FROM stations AS s, material AS m, materialid
                              WHERE
                                s.Counter = m.Counter
                                AND
                                m.MaterialID = materialid.MaterialID
                                AND
                                materialid.Workorder = $workid
                                AND
                                s.Start = 0
                        )
                    GROUP BY Part, StationName";

			using (var countCmd = _connection.CreateCommand())
			using (var serialCmd = _connection.CreateCommand())
            using (var finalizedCmd = _connection.CreateCommand())
            using (var timeCmd = _connection.CreateCommand())
			{
				countCmd.CommandText = countQry;
				countCmd.Parameters.Add("workid", SqliteType.Text);
				serialCmd.CommandText = serialQry;
				serialCmd.Parameters.Add("workid", SqliteType.Text);
                finalizedCmd.CommandText = finalizedQry;
                finalizedCmd.Parameters.Add("workid", SqliteType.Text);
                finalizedCmd.Parameters.Add("workloc", SqliteType.Integer)
                    .Value = (int)MachineWatchInterface.LogType.FinalizeWorkorder;
                timeCmd.CommandText = timeQry;
                timeCmd.Parameters.Add("workid", SqliteType.Text);

				var trans = _connection.BeginTransaction();
				try
				{
					countCmd.Transaction = trans;
					serialCmd.Transaction = trans;
                    finalizedCmd.Transaction = trans;
                    timeCmd.Transaction = trans;

					var ret = new List<MachineWatchInterface.WorkorderSummary>();
                    var partMap = new Dictionary<string, MachineWatchInterface.WorkorderPartSummary>();
					foreach (var w in workorders)
					{
						var summary = new MachineWatchInterface.WorkorderSummary();
						summary.WorkorderId = w;
                        partMap.Clear();

						countCmd.Parameters[0].Value = w;
                        using (var reader = countCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var wPart = new MachineWatchInterface.WorkorderPartSummary
                                {
                                    Part = reader.GetString(0),
                                    PartsCompleted = reader.GetInt32(1)
                                };
                                summary.Parts.Add(wPart);
                                partMap.Add(wPart.Part, wPart);
                            }
                        }

						serialCmd.Parameters[0].Value = w;
						using (var reader = serialCmd.ExecuteReader())
						{
							while (reader.Read())
								summary.Serials.Add(reader.GetString(0));
						}

                        finalizedCmd.Parameters[0].Value = w;
                        using (var reader = finalizedCmd.ExecuteReader())
                        {
                            if (reader.Read() && !reader.IsDBNull(0))
                            {
                                summary.FinalizedTimeUTC =
                                  new DateTime(reader.GetInt64(0), DateTimeKind.Utc);
                            }
                        }

                        timeCmd.Parameters[0].Value = w;
                        using (var reader = timeCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var partName = reader.GetString(0);
                                var stat = reader.GetString(1);
                                //part name should exist because material query should return it
                                if (partMap.ContainsKey(partName))
                                {
                                    var detail = partMap[partName];
                                    if (!reader.IsDBNull(2))
                                        detail.ElapsedStationTime[stat] = TimeSpan.FromTicks((long)reader.GetDecimal(2));
                                    if (!reader.IsDBNull(3))
                                        detail.ActiveStationTime[stat] = TimeSpan.FromTicks((long)reader.GetDecimal(3));
                                }
                            }
                        }

						ret.Add(summary);
					}

					trans.Commit();
					return ret;
				}
				catch
				{
					trans.Rollback();
					throw;
				}
			}
		}

        #endregion

        #region Adding
        public MachineWatchInterface.LogEntry AddLogEntry(MachineWatchInterface.LogEntry log)
        {
            return AddStationCycle(log, null, null);
        }

        public MachineWatchInterface.LogEntry AddStationCycle(MachineWatchInterface.LogEntry log, string foreignID)
        {
            return AddStationCycle(log, foreignID, null);
        }

        public MachineWatchInterface.LogEntry AddStationCycle(MachineWatchInterface.LogEntry log, string foreignID, string origMessage)
        {
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();

                try
                {
                    log = AddStationCycle(trans, log, foreignID, origMessage);
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }

            if (NewLogEntry != null)
                NewLogEntry(log, foreignID);

            return log;
        }

        public void AddStationCycles(IEnumerable<MachineWatchInterface.LogEntry> logs, string foreignID, string origMessage)
        {
            var results = new List<MachineWatchInterface.LogEntry>();
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();

                try
                {
                    foreach (var log in logs)
                        results.Add(AddStationCycle(trans, log, foreignID, origMessage));
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }

            if (NewLogEntry != null)
                foreach (var log in results)
                    NewLogEntry(log, foreignID);
        }

        public MachineWatchInterface.LogEntry AddStationCycle(IDbTransaction trans, MachineWatchInterface.LogEntry log, string foreignID, string origMessage)
        {
            var cmd = _connection.CreateCommand();
            ((IDbCommand)cmd).Transaction = trans;

            cmd.CommandText = "INSERT INTO stations(Pallet, StationLoc, StationName, StationNum, Program, Start, TimeUTC, Result, EndOfRoute, Elapsed, ActiveTime, ForeignID,OriginalMessage)" +
                "VALUES ($pal,$loc,$locname,$locnum,$prog,$start,$time,$result,$end,$elapsed,$active,$foreign,$orig)";

            cmd.Parameters.Add("pal", SqliteType.Text).Value = log.Pallet;
            cmd.Parameters.Add("loc", SqliteType.Integer).Value = (int)log.LogType;
            cmd.Parameters.Add("locname", SqliteType.Text).Value = log.LocationName;
            cmd.Parameters.Add("locnum", SqliteType.Integer).Value = log.LocationNum;
            cmd.Parameters.Add("prog", SqliteType.Text).Value = log.Program;
            cmd.Parameters.Add("start", SqliteType.Integer).Value = log.StartOfCycle;
            cmd.Parameters.Add("time", SqliteType.Integer).Value = log.EndTimeUTC.Ticks;
            cmd.Parameters.Add("result", SqliteType.Text).Value = log.Result;
            cmd.Parameters.Add("end", SqliteType.Integer).Value = log.EndOfRoute;
            if (log.ElapsedTime.Ticks >= 0)
                cmd.Parameters.Add("elapsed", SqliteType.Integer).Value = log.ElapsedTime.Ticks;
            else
                cmd.Parameters.Add("elapsed", SqliteType.Integer).Value = DBNull.Value;
            if (log.ActiveOperationTime.Ticks > 0)
                cmd.Parameters.Add("active", SqliteType.Integer).Value = log.ActiveOperationTime.Ticks;
            else
                cmd.Parameters.Add("active", SqliteType.Integer).Value = DBNull.Value;
            if (foreignID == null || foreignID == "")
                cmd.Parameters.Add("foreign", SqliteType.Text).Value = DBNull.Value;
            else
                cmd.Parameters.Add("foreign", SqliteType.Text).Value = foreignID;
            if (origMessage == null || origMessage == "")
                cmd.Parameters.Add("orig", SqliteType.Text).Value = DBNull.Value;
            else
                cmd.Parameters.Add("orig", SqliteType.Text).Value = origMessage;

            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT last_insert_rowid()";
            cmd.Parameters.Clear();
            long ctr = (long)cmd.ExecuteScalar();

            AddMaterial(ctr, log.Material, trans);
            AddProgramDetail(ctr, log.ProgramDetails, trans);

            return new MachineWatchInterface.LogEntry(log, ctr);
        }

        private void AddMaterial(long counter, IEnumerable<MachineWatchInterface.LogMaterial> mat, IDbTransaction trans)
        {
            var cmd = _connection.CreateCommand();
            ((IDbCommand)cmd).Transaction = trans;

            cmd.CommandText = "INSERT INTO material(Counter,MaterialID,UniqueStr,Process,Part,NumProcess,Face)" +
           "VALUES($cntr,$mat,$unique,$proc,$part,$numproc,$face)";
            cmd.Parameters.Add("cntr", SqliteType.Integer).Value = counter;
            cmd.Parameters.Add("mat", SqliteType.Integer);
            cmd.Parameters.Add("unique", SqliteType.Text);
            cmd.Parameters.Add("proc", SqliteType.Integer);
            cmd.Parameters.Add("part", SqliteType.Text);
            cmd.Parameters.Add("numproc", SqliteType.Integer);
            cmd.Parameters.Add("face", SqliteType.Text);

            foreach (var m in mat)
            {
                cmd.Parameters[1].Value = m.MaterialID;
                cmd.Parameters[2].Value = m.JobUniqueStr;
                cmd.Parameters[3].Value = m.Process;
                cmd.Parameters[4].Value = m.PartName;
                cmd.Parameters[5].Value = m.NumProcesses;
                cmd.Parameters[6].Value = m.Face;
                cmd.ExecuteNonQuery();
            }
        }

        private void AddProgramDetail(long counter, IDictionary<string, string> details, IDbTransaction trans)
        {
            var cmd = _connection.CreateCommand();
            ((IDbCommand)cmd).Transaction = trans;

            cmd.CommandText = "INSERT INTO program_details(Counter,Key,Value) VALUES($cntr,$key,$val)";
            cmd.Parameters.Add("cntr", SqliteType.Integer).Value = counter;
            cmd.Parameters.Add("key", SqliteType.Text);
            cmd.Parameters.Add("val", SqliteType.Text);

            foreach (var pair in details)
            {
                cmd.Parameters[1].Value = pair.Key;
                cmd.Parameters[2].Value = pair.Value;
                cmd.ExecuteNonQuery();
            }
        }
        #endregion

        #region Material Tags

        public long AllocateMaterialID(string unique)
        {
            lock (_lock)
            {
                using (var cmd = _connection.CreateCommand()) {
                    var trans = _connection.BeginTransaction();
                    cmd.Transaction = trans;
                    try {
                        cmd.CommandText = "INSERT INTO materialid(UniqueStr) VALUES ($uniq)";
                        cmd.Parameters.Add("uniq", SqliteType.Text).Value = unique;
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "SELECT last_insert_rowid()";
                        cmd.Parameters.Clear();
                        var matID = (long)cmd.ExecuteScalar();
                        trans.Commit();
                        return matID;
                    } catch {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public void CreateMaterialID(long matID, string unique)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO materialid(MaterialID, UniqueStr) VALUES ($mid, $uniq)";
                cmd.Parameters.Add("mid", SqliteType.Integer).Value = matID;
                cmd.Parameters.Add("uniq", SqliteType.Text).Value = unique;
                cmd.ExecuteNonQuery();
            }
        }

        public string JobUniqueStrFromMaterialID(long matID)
        {
            lock (_lock)
            {
                string unique = "";

                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT UniqueStr FROM materialid WHERE MaterialID = $mat";
                cmd.Parameters.Add("mat", SqliteType.Integer).Value = matID;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        unique = reader.GetString(0);
                    }
                }

                return unique;
            }
        }


        public MachineWatchInterface.LogEntry RecordSerialForMaterialID(MachineWatchInterface.LogMaterial mat, string serial)
        {
            return RecordSerialForMaterialID(mat, serial, DateTime.UtcNow);
        }

        public MachineWatchInterface.LogEntry RecordSerialForMaterialID(MachineWatchInterface.LogMaterial mat, string serial, DateTime endTimeUTC)
        {
            var log = new MachineWatchInterface.LogEntry(-1,
                                                             new MachineWatchInterface.LogMaterial[] { mat },
                                                             "",
                                                             MachineWatchInterface.LogType.PartMark, "Mark", 1,
                                                             "MARK",
                                                             false,
                                                             endTimeUTC,
                                                             serial,
                                                             false);
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();
                try
                {
                    RecordSerialForMaterialID(trans, mat.MaterialID, serial);
                    log = AddStationCycle(trans, log, "", "");
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
            if (NewLogEntry != null)
                NewLogEntry(log, "");
            return log;
        }

        private void RecordSerialForMaterialID(IDbTransaction trans, long matID, string serial)
        {
            var cmd = _connection.CreateCommand();
            ((IDbCommand)cmd).Transaction = trans;
            cmd.CommandText = "UPDATE materialid SET Serial = $ser WHERE MaterialID = $mat";
            if (string.IsNullOrEmpty(serial))
                cmd.Parameters.Add("ser", SqliteType.Text).Value = DBNull.Value;
            else
                cmd.Parameters.Add("ser", SqliteType.Text).Value = serial;
            cmd.Parameters.Add("mat", SqliteType.Integer).Value = matID;
            cmd.ExecuteNonQuery();
        }

        public string SerialForMaterialID(long matID)
        {
            lock (_lock)
            {
                string ser = "";

                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Serial FROM materialid WHERE MaterialID = $mat";
                cmd.Parameters.Add("mat", SqliteType.Integer).Value = matID;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                            ser = reader.GetString(0);
                    }
                }

                return ser;
            }
        }

        public MachineWatchInterface.LogEntry RecordWorkorderForMaterialID(MachineWatchInterface.LogMaterial mat, string workorder)
        {
            return RecordWorkorderForMaterialID(mat, workorder, DateTime.UtcNow);
        }

        public MachineWatchInterface.LogEntry RecordWorkorderForMaterialID(MachineWatchInterface.LogMaterial mat, string workorder, DateTime recordUtc)
        {
                var log = new MachineWatchInterface.LogEntry(-1,
                                                             new MachineWatchInterface.LogMaterial[] { mat },
                                                             "",
                                                             MachineWatchInterface.LogType.OrderAssignment, "Order", 1,
                                                             "",
                                                             false,
                                                             recordUtc,
                                                             workorder,
                                                             false);
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();
                try
                {
                    RecordWorkorderForMaterialID(trans, mat.MaterialID, workorder);
                    log = AddStationCycle(trans, log, "", "");
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
            if (NewLogEntry != null)
                NewLogEntry(log, "");
            return log;
        }

        public MachineWatchInterface.LogEntry RecordInspectionCompleted(
            MachineWatchInterface.LogMaterial mat,
            int inspectionLocNum,
            string inspectionType,
            bool success,
            IDictionary<string, string> extraData,
            TimeSpan elapsed,
            TimeSpan active)
        {
            return RecordInspectionCompleted(
                mat,
                inspectionLocNum,
                inspectionType,
                success,
                extraData,
                elapsed,
                active,
                DateTime.UtcNow
            );
        }

        public MachineWatchInterface.LogEntry RecordInspectionCompleted(
            MachineWatchInterface.LogMaterial mat,
            int inspectionLocNum,
            string inspectionType,
            bool success,
            IDictionary<string, string> extraData,
            TimeSpan elapsed,
            TimeSpan active,
            DateTime endUTC)
        {
            var log = new MachineWatchInterface.LogEntry(
                -1,
                new MachineWatchInterface.LogMaterial[] { mat },
                "",
                MachineWatchInterface.LogType.InspectionResult, "Inspection", inspectionLocNum,
                inspectionType,
                false,
                endUTC,
                success.ToString(),
                false,
                elapsed,
                active);
            foreach (var x in extraData) log.ProgramDetails.Add(x.Key, x.Value);

            lock (_lock)
            {
                var trans = _connection.BeginTransaction();
                try
                {
                    log = AddStationCycle(trans, log, "", "");
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
            if (NewLogEntry != null)
                NewLogEntry(log, "");
            return log;
        }

        public MachineWatchInterface.LogEntry RecordWashCompleted(
            MachineWatchInterface.LogMaterial mat,
            int washLocNum,
            IDictionary<string, string> extraData,
            TimeSpan elapsed,
            TimeSpan active)
        {
            return RecordWashCompleted(
                mat,
                washLocNum,
                extraData,
                elapsed,
                active,
                DateTime.UtcNow
            );
        }

        public MachineWatchInterface.LogEntry RecordWashCompleted(
            MachineWatchInterface.LogMaterial mat,
            int washLocNum,
            IDictionary<string, string> extraData,
            TimeSpan elapsed,
            TimeSpan active,
            DateTime endUTC)
        {
            var log = new MachineWatchInterface.LogEntry(
                -1,
                new MachineWatchInterface.LogMaterial[] { mat },
                "",
                MachineWatchInterface.LogType.Wash, "Wash", washLocNum,
                "",
                false,
                endUTC,
                "",
                false,
                elapsed,
                active);
            foreach (var x in extraData) log.ProgramDetails.Add(x.Key, x.Value);

            lock (_lock)
            {
                var trans = _connection.BeginTransaction();
                try
                {
                    log = AddStationCycle(trans, log, "", "");
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
            if (NewLogEntry != null)
                NewLogEntry(log, "");
            return log;
        }

        private void RecordWorkorderForMaterialID(IDbTransaction trans, long matID, string workorder)
        {
            var cmd = _connection.CreateCommand();
            ((IDbCommand)cmd).Transaction = trans;
            cmd.CommandText = "UPDATE materialid SET Workorder = $work WHERE MaterialID = $mat";
            if (string.IsNullOrEmpty(workorder))
                cmd.Parameters.Add("work", SqliteType.Text).Value = DBNull.Value;
            else
                cmd.Parameters.Add("work", SqliteType.Text).Value = workorder;
            cmd.Parameters.Add("mat", SqliteType.Integer).Value = matID;
            cmd.ExecuteNonQuery();
        }

        public MachineWatchInterface.LogEntry RecordFinalizedWorkorder(string workorder)
        {
            return RecordFinalizedWorkorder(workorder, DateTime.UtcNow);
        }

        public MachineWatchInterface.LogEntry RecordFinalizedWorkorder(string workorder, DateTime finalizedUTC)
        {
            var log = new MachineWatchInterface.LogEntry(
                cntr: -1,
                mat: new MachineWatchInterface.LogMaterial[] {},
                pal: "",
                ty: MachineWatchInterface.LogType.FinalizeWorkorder,
                locName: "FinalizeWorkorder",
                locNum: 1,
                prog: "",
                start: false,
                endTime: finalizedUTC,
                result: workorder,
                endOfRoute: false);
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();
                try
                {
                    log = AddStationCycle(trans, log, "", "");
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
            if (NewLogEntry != null)
                NewLogEntry(log, "");
            return log;
        }

        public string WorkorderForMaterialID(long matID)
        {
            lock (_lock)
            {
                string workId = "";

                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Workorder FROM materialid WHERE MaterialID = $mat";
                cmd.Parameters.Add("mat", SqliteType.Integer).Value = matID;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                            workId = reader.GetString(0);
                    }
                }

                return workId;
            }
        }

        #endregion

        #region Pending Loads

        public void AddPendingLoad(string pal, string key, int load, TimeSpan elapsed, TimeSpan active, string foreignID)
        {
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();

                try
                {
                    var cmd = _connection.CreateCommand();
                    cmd.Transaction = trans;

                    cmd.CommandText = "INSERT INTO pendingloads(Pallet, Key, LoadStation, Elapsed, ActiveTime, ForeignID)" +
                        "VALUES ($pal,$key,$load,$elapsed,$active,$foreign)";

                    cmd.Parameters.Add("pal", SqliteType.Text).Value = pal;
                    cmd.Parameters.Add("key", SqliteType.Text).Value = key;
                    cmd.Parameters.Add("load", SqliteType.Integer).Value = load;
                    cmd.Parameters.Add("elapsed", SqliteType.Integer).Value = elapsed.Ticks;
                    cmd.Parameters.Add("active", SqliteType.Integer).Value = active.Ticks;
                    cmd.Parameters.Add("foreign", SqliteType.Text).Value = foreignID;

                    cmd.ExecuteNonQuery();

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
        }

        public struct PendingLoad
        {
            public string Pallet;
            public string Key;
            public int LoadStation;
            public TimeSpan Elapsed;
            public TimeSpan ActiveOperationTime;
            public string ForeignID;
        }

        public List<PendingLoad> PendingLoads(string pallet)
        {
            lock (_lock)
            {
                var ret = new List<PendingLoad>();

                var trans = _connection.BeginTransaction();
                try
                {
                    var cmd = _connection.CreateCommand();
                    cmd.Transaction = trans;

                    cmd.CommandText = "SELECT Key, LoadStation, Elapsed, ActiveTime, ForeignID FROM pendingloads WHERE Pallet = $pal";
                    cmd.Parameters.Add("pal", SqliteType.Text).Value = pallet;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var p = default(PendingLoad);
                            p.Pallet = pallet;
                            p.Key = reader.GetString(0);
                            p.LoadStation = reader.GetInt32(1);
                            p.Elapsed = new TimeSpan(reader.GetInt64(2));
                            if (!reader.IsDBNull(3))
                                p.ActiveOperationTime = TimeSpan.FromTicks(reader.GetInt64(3));
                            p.ForeignID = reader.GetString(4);
                            ret.Add(p);
                        }
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }

                return ret;
            }
        }

        public void CompletePalletCycle(string pal, DateTime timeUTC, string foreignID)
        {
            CompletePalletCycle(pal, timeUTC, foreignID, null, SerialType.NoAutomaticSerials, 10);
        }

        public void CompletePalletCycle(string pal, DateTime timeUTC, string foreignID,
                                        IDictionary<string, IEnumerable<MachineWatchInterface.LogMaterial>> mat,
                                        SerialType serialType, int serLength)
        {
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();

                var newEvts = new List<BlackMaple.MachineWatchInterface.LogEntry>();
                try
                {

                    var lastTimeCmd = _connection.CreateCommand();
                    lastTimeCmd.CommandText = "SELECT TimeUTC FROM stations where Pallet = $pal AND Result = 'PalletCycle' " +
                                            "ORDER BY Counter DESC LIMIT 1";
                    lastTimeCmd.Parameters.Add("pal", SqliteType.Text).Value = pal;

                    var elapsedTime = TimeSpan.Zero;
                    var lastCycleTime = lastTimeCmd.ExecuteScalar();
                    if (lastCycleTime != null && lastCycleTime != DBNull.Value)
                        elapsedTime = timeUTC.Subtract(new DateTime((long)lastCycleTime, DateTimeKind.Utc));

                    if (lastCycleTime == null || lastCycleTime == DBNull.Value || elapsedTime != TimeSpan.Zero)
                    {
                        newEvts.Add(AddStationCycle(trans, new BlackMaple.MachineWatchInterface.LogEntry(
                            cntr: -1,
                            mat: new List<BlackMaple.MachineWatchInterface.LogMaterial>(),
                            pal: pal,
                            ty: BlackMaple.MachineWatchInterface.LogType.PalletCycle,
                            locName: "Pallet Cycle",
                            locNum: 1,
                            prog: "",
                            start: false,
                            endTime: timeUTC,
                            result: "PalletCycle",
                            endOfRoute: false,
                            elapsed: elapsedTime,
                            active: TimeSpan.Zero
                        ), foreignID, null));
                    }

                    if (mat == null)
                    {
                        trans.Commit();
                        foreach (var e in newEvts)
                            NewLogEntry?.Invoke(e, foreignID);
                        return;
                    }

                    // Copy over pending loads
                    var loadPending = _connection.CreateCommand();
                    loadPending.Transaction = trans;
                    loadPending.CommandText = "SELECT Key, LoadStation, Elapsed, ActiveTime, ForeignID FROM pendingloads WHERE Pallet = $pal";
                    loadPending.Parameters.Add("pal", SqliteType.Text).Value = pal;

                    using (var reader = loadPending.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var key = reader.GetString(0);
                            if (mat.ContainsKey(key))
                            {
                                newEvts.Add(AddStationCycle(trans, new BlackMaple.MachineWatchInterface.LogEntry(
                                    cntr: -1,
                                    mat: mat[key],
                                    pal: pal,
                                    ty: BlackMaple.MachineWatchInterface.LogType.LoadUnloadCycle,
                                    locName: "Load",
                                    locNum: reader.GetInt32(1),
                                    prog: "",
                                    start: false,
                                    endTime: timeUTC.AddSeconds(1),
                                    result: "LOAD",
                                    endOfRoute: false,
                                    elapsed: TimeSpan.FromTicks(reader.GetInt64(2)),
                                    active: reader.IsDBNull(3) ? TimeSpan.Zero : TimeSpan.FromTicks(reader.GetInt64(3))
                                ),
                                foreignID: reader.GetString(4),
                                origMessage: null));

                                if (serialType == SerialType.AssignOneSerialPerCycle)
                                {

                                    // find a material id to use to create the serial
                                    long matID = -1;
                                    foreach (var m in mat[key])
                                    {
                                        if (m.MaterialID >= 0)
                                        {
                                            matID = m.MaterialID;
                                            break;
                                        }
                                    }
                                    if (matID >= 0)
                                    {
                                        var serial = ConvertToBase62(matID);
                                        serial = serial.Substring(0, Math.Min(serLength, serial.Length));
                                        serial = serial.PadLeft(serLength, '0');
                                        newEvts.Add(AddStationCycle(trans, new MachineWatchInterface.LogEntry(-1,
                                            mat[key],
                                            "",
                                            MachineWatchInterface.LogType.PartMark, "Mark", 1,
                                            "MARK",
                                            false,
                                            timeUTC.AddSeconds(2),
                                            serial,
                                            false), null, null));
                                        // add the serial
                                        foreach (var m in mat[key])
                                        {
                                            if (m.MaterialID >= 0)
                                                RecordSerialForMaterialID(trans, m.MaterialID, serial);
                                        }
                                    }

                                }
                                else if (serialType == SerialType.AssignOneSerialPerMaterial)
                                {
                                    foreach (var m in mat[key])
                                    {
                                        var serial = ConvertToBase62(m.MaterialID);
                                        serial = serial.Substring(0, Math.Min(serLength, serial.Length));
                                        serial = serial.PadLeft(serLength, '0');
                                        if (m.MaterialID < 0) continue;
                                        newEvts.Add(AddStationCycle(trans, new MachineWatchInterface.LogEntry(-1,
                                            new BlackMaple.MachineWatchInterface.LogMaterial[] {m},
                                            "",
                                            MachineWatchInterface.LogType.PartMark, "Mark", 1,
                                            "MARK",
                                            false,
                                            timeUTC.AddSeconds(2),
                                            serial,
                                            false), null, null));
                                        RecordSerialForMaterialID(trans, m.MaterialID, serial);
                                    }

                                }
                            }
                        }
                    }

                    var delCmd = _connection.CreateCommand();
                    delCmd.Transaction = trans;
                    delCmd.CommandText = "DELETE FROM pendingloads WHERE Pallet = $pal";
                    delCmd.Parameters.Add("pal", SqliteType.Text).Value = pal;
                    delCmd.ExecuteNonQuery();

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }

                foreach (var e in newEvts)
                    NewLogEntry?.Invoke(e, foreignID);
            }
        }

        public static string ConvertToBase62(long num)
        {
            string baseChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

            string res = "";
            long cur = num;

            while (cur > 0)
            {
                long quotient = cur / 62;
                int remainder = (int)cur % 62;

                res = baseChars[remainder] + res;
                cur = quotient;
            }

            return res;
        }
        #endregion

        #region Inspection Counts

        private MachineWatchInterface.InspectCount QueryCount(IDbTransaction trans, string counter, int maxVal)
        {
            var cnt = new MachineWatchInterface.InspectCount();
            cnt.Counter = counter;

            using (var cmd = _connection.CreateCommand()) {
                ((IDbCommand)cmd).Transaction = trans;
                cmd.CommandText = "SELECT Val, LastUTC FROM inspection_counters WHERE Counter = $cntr";
                cmd.Parameters.Add("cntr", SqliteType.Text).Value = counter;

                using (IDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        cnt.Value = reader.GetInt32(0);
                        if (reader.IsDBNull(1))
                            cnt.LastUTC = DateTime.MaxValue;
                        else
                            cnt.LastUTC = new DateTime(reader.GetInt64(1), DateTimeKind.Utc);

                    }
                    else
                    {
                        if (maxVal <= 1)
                            cnt.Value = 0;
                        else
                            cnt.Value = _rand.Next(0, maxVal - 1);

                        cnt.LastUTC = DateTime.MaxValue;
                    }
                }
            }

            return cnt;
        }

        public List<MachineWatchInterface.InspectCount> LoadInspectCounts()
        {
            lock (_lock)
            {
                List<MachineWatchInterface.InspectCount> ret = new List<MachineWatchInterface.InspectCount>();

                using (var cmd = _connection.CreateCommand()) {
                    cmd.CommandText = "SELECT Counter, Val, LastUTC FROM inspection_counters";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var insp = default(MachineWatchInterface.InspectCount);
                            insp.Counter = reader.GetString(0);
                            insp.Value = reader.GetInt32(1);
                            if (reader.IsDBNull(2))
                                insp.LastUTC = DateTime.MaxValue;
                            else
                                insp.LastUTC = new DateTime(reader.GetInt64(2), DateTimeKind.Utc);
                            ret.Add(insp);
                        }
                    }
                }

                return ret;
            }
        }

        private void SetInspectionCount(IDbTransaction trans, MachineWatchInterface.InspectCount cnt)
        {
            using (var cmd = _connection.CreateCommand())
            {
                ((IDbCommand)cmd).Transaction = trans;
                cmd.CommandText = "INSERT OR REPLACE INTO inspection_counters(Counter,Val,LastUTC) VALUES ($cntr,$val,$time)";
                cmd.Parameters.Add("cntr", SqliteType.Text).Value = cnt.Counter;
                cmd.Parameters.Add("val", SqliteType.Integer).Value = cnt.Value;
                cmd.Parameters.Add("time", SqliteType.Integer).Value = cnt.LastUTC.Ticks;
                cmd.ExecuteNonQuery();
            }
        }

        public void SetInspectCounts(IEnumerable<MachineWatchInterface.InspectCount> counts)
        {
            lock (_lock)
            {
                using (var cmd = _connection.CreateCommand()) {
                    cmd.CommandText = "INSERT OR REPLACE INTO inspection_counters(Counter, Val, LastUTC) VALUES ($cntr,$val,$last)";
                    cmd.Parameters.Add("cntr", SqliteType.Text);
                    cmd.Parameters.Add("val", SqliteType.Integer);
                    cmd.Parameters.Add("last", SqliteType.Integer);

                    var trans = _connection.BeginTransaction();
                    try
                    {
                        cmd.Transaction = trans;

                        foreach (var insp in counts)
                        {
                            cmd.Parameters[0].Value = insp.Counter;
                            cmd.Parameters[1].Value = insp.Value;
                            cmd.Parameters[2].Value = insp.LastUTC.Ticks;
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }
        #endregion

        #region Inspection Translation
        public class MaterialProcessActualPath
        {
            public class Stop
            {
                public string StationName {get;set;}
                public int StationNum {get;set;}
            }

            public long MaterialID {get;set;}
            public int Process {get;set;}
            public string Pallet {get;set;}
            public int LoadStation {get;set;}
            public List<Stop> Stops {get;set;}
            public int UnloadStation {get;set;}
        }

        private Dictionary<int, MaterialProcessActualPath> LookupActualPath(IDbTransaction trans, long matID)
        {
            var byProc = new Dictionary<int, MaterialProcessActualPath>();
            MaterialProcessActualPath getPath(int proc)
            {
                if (byProc.ContainsKey(proc))
                    return byProc[proc];
                else {
                    var m = new MaterialProcessActualPath() {
                        MaterialID = matID,
                        Process = proc,
                        Pallet = null,
                        LoadStation = -1,
                        Stops = new List<MaterialProcessActualPath.Stop>(),
                        UnloadStation = -1
                    };
                    byProc.Add(proc, m);
                    return m;
                }
            }

            var cmd = _connection.CreateCommand();
            ((IDbCommand)cmd).Transaction = trans;
            cmd.CommandText = "SELECT Pallet, StationLoc, StationName, StationNum, Process " +
                " FROM stations " +
                " INNER JOIN material ON stations.Counter = material.Counter " +
                " WHERE " +
                "    MaterialID = $mat AND Start = 0 " +
                "    AND (StationLoc = $ty1 OR StationLoc = $ty2) " +
                " ORDER BY stations.Counter ASC";
            cmd.Parameters.Add("mat", SqliteType.Integer).Value = matID;
            cmd.Parameters.Add("ty1", SqliteType.Integer).Value = (int)MachineWatchInterface.LogType.LoadUnloadCycle;
            cmd.Parameters.Add("ty2", SqliteType.Integer).Value = (int)MachineWatchInterface.LogType.MachineCycle;

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    //for each log entry, we search for a matching route stop in the job
                    //if we find one, we replace the counter in the program
                    string pal = reader.GetString(0);
                    var logTy = (MachineWatchInterface.LogType)reader.GetInt32(1);
                    string statName = reader.GetString(2);
                    int statNum = reader.GetInt32(3);
                    int process = reader.GetInt32(4);

                    var mat = getPath(process);

                    if (!string.IsNullOrEmpty(pal))
                        mat.Pallet = pal;

                    switch (logTy)
                    {
                        case MachineWatchInterface.LogType.LoadUnloadCycle:
                            if (mat.LoadStation == -1)
                                mat.LoadStation = statNum;
                            else
                                mat.UnloadStation = statNum;
                            break;

                        case MachineWatchInterface.LogType.MachineCycle:
                            mat.Stops.Add(new MaterialProcessActualPath.Stop() {
                                StationName = statName, StationNum = statNum});
                            break;
                    }
                }
            }

            return byProc;
        }

        private string TranslateInspectionCounter(long matID, Dictionary<int, MaterialProcessActualPath> actualPath, string counter)
        {
            foreach (var p in actualPath.Values)
            {
                counter = counter.Replace(
                    MachineWatchInterface.JobInspectionData.PalletFormatFlag(p.Process),
                    p.Pallet
                );
                counter = counter.Replace(
                    MachineWatchInterface.JobInspectionData.LoadFormatFlag(p.Process),
                    p.LoadStation.ToString()
                );
                counter = counter.Replace(
                    MachineWatchInterface.JobInspectionData.UnloadFormatFlag(p.Process),
                    p.UnloadStation.ToString()
                );
                for (int stopNum = 1; stopNum <= p.Stops.Count; stopNum++) {
                    counter = counter.Replace(
                        MachineWatchInterface.JobInspectionData.StationFormatFlag(p.Process, stopNum),
                        p.Stops[stopNum-1].StationNum.ToString()
                    );
                }
            }
            return counter;
        }
        #endregion

        #region Inspection Decisions

        public class Decision
        {
            public long MaterialID;
            public string InspType;
            public string Counter;
            public bool Inspect;
            public bool Forced;
            public System.DateTime CreateUTC;
        }
        public IList<Decision> LookupInspectionDecisions(long matID)
        {
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();
                try {
                    var ret = LookupInspectionDecisions(trans, matID);
                    trans.Commit();
                    return ret;
                } catch {
                    trans.Rollback();
                    throw;
                }
            }

        }

        private IList<Decision> LookupInspectionDecisions(IDbTransaction trans, long matID)
        {
            List<Decision> ret = new List<Decision>();
            var cmd = _connection.CreateCommand();
            ((IDbCommand)cmd).Transaction = trans;
            cmd.CommandText = "SELECT Counter, StationLoc, Program, TimeUTC, Result " +
                " FROM stations " +
                " WHERE " +
                "    Counter IN (SELECT Counter FROM material WHERE MaterialID = $mat) " +
                "    AND (StationLoc = $loc1 OR StationLoc = $loc2) " +
                " ORDER BY Counter ASC";
            cmd.Parameters.Add("$mat", SqliteType.Integer).Value = matID;
            cmd.Parameters.Add("$loc1", SqliteType.Integer).Value = MachineWatchInterface.LogType.InspectionForce;
            cmd.Parameters.Add("$loc2", SqliteType.Integer).Value = MachineWatchInterface.LogType.Inspection;

            var detailCmd = _connection.CreateCommand();
            ((IDbCommand)detailCmd).Transaction = trans;
            detailCmd.CommandText = "SELECT Value FROM program_details WHERE Counter = $cntr AND Key = 'InspectionType'";
            detailCmd.Parameters.Add("cntr", SqliteType.Integer);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var cntr = reader.GetInt64(0);
                    var logTy = (MachineWatchInterface.LogType)reader.GetInt32(1);
                    var prog = reader.GetString(2);
                    var timeUtc = new DateTime(reader.GetInt64(3), DateTimeKind.Utc);
                    var result = reader.GetString(4);
                    var inspect = false;
                    bool.TryParse(result, out inspect);

                    if (logTy == MachineWatchInterface.LogType.Inspection)
                    {
                        detailCmd.Parameters[0].Value = cntr;
                        var inspVal = detailCmd.ExecuteScalar();
                        string inspType;
                        if (inspVal != null)
                        {
                            inspType = inspVal.ToString();
                        } else {
                            // old code didn't record in details, so assume the counter is in a specific format
                            var parts = prog.Split(',');
                            if (parts.Length >= 2)
                                inspType = parts[1];
                            else
                                inspType = "";
                        }
                        ret.Add(new Decision() {
                            MaterialID = matID,
                            InspType = inspType,
                            Counter = prog,
                            Inspect = inspect,
                            Forced = false,
                            CreateUTC = timeUtc
                        });
                    } else {
                        ret.Add(new Decision() {
                            MaterialID = matID,
                            InspType = prog,
                            Counter = "",
                            Inspect = inspect,
                            Forced = true,
                            CreateUTC = timeUtc
                        });
                    }
                }
            }
            return ret;
        }

        public void MakeInspectionDecisions(
            long matID,
            MachineWatchInterface.JobPlan job,
            int process,
            IEnumerable<MachineWatchInterface.JobInspectionData> inspections,
            DateTime? mutcNow = null)
        {
            MakeInspectionDecisions(matID, job.UniqueStr, job.PartName, process, inspections, mutcNow);
        }

        public void MakeInspectionDecisions(
            long matID,
            string uniqueStr,
            string partName,
            int process,
            IEnumerable<MachineWatchInterface.JobInspectionData> inspections,
            DateTime? mutcNow = null)
        {
            List<MachineWatchInterface.LogEntry> logs;
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();
                try {
                    logs = MakeInspectionDecisions(trans, matID, uniqueStr, partName, process, inspections, mutcNow);
                    trans.Commit();
                } catch {
                    trans.Rollback();
                    throw;
                }
            }

            foreach (var l in logs)
                NewLogEntry?.Invoke(l, null);
        }

        private List<MachineWatchInterface.LogEntry> MakeInspectionDecisions(
            IDbTransaction trans,
            long matID,
            string uniqueStr,
            string partName,
            int process,
            IEnumerable<MachineWatchInterface.JobInspectionData> inspections,
            DateTime? mutcNow)
        {
            var utcNow = mutcNow ?? DateTime.UtcNow;
            var logEntries = new List<MachineWatchInterface.LogEntry>();

            var actualPath = LookupActualPath(trans, matID);

            var decisions =
                LookupInspectionDecisions(trans, matID)
                .ToLookup(d => d.InspType, d => d);

            Dictionary<string, MachineWatchInterface.JobInspectionData> insps;
            if (inspections == null)
                insps = new Dictionary<string, MachineWatchInterface.JobInspectionData>();
            else
                insps = inspections.ToDictionary(x => x.InspectionType, x => x);


            var inspsToCheck = decisions.Select(x => x.Key).Union(insps.Keys).Distinct();
            foreach (var inspType in inspsToCheck)
            {
                bool inspect = false;
                string counter = "";
                bool alreadyRecorded = false;

                MachineWatchInterface.JobInspectionData iProg = null;
                if (insps.ContainsKey(inspType)) {
                    iProg = insps[inspType];
                    counter = TranslateInspectionCounter(matID, actualPath, iProg.Counter);
                }


                if (decisions.Contains(inspType)) {
                    // use the decision
                    foreach (var d in decisions[inspType]) {
                        inspect = inspect || d.Inspect;
                        alreadyRecorded = alreadyRecorded || !d.Forced;
                    }

                }

                if (!alreadyRecorded && iProg != null) {
                    // use the counter
                    var currentCount = QueryCount(trans, counter, iProg.MaxVal);
                    if (iProg.MaxVal > 0)
                    {
                        currentCount.Value += 1;

                        if (currentCount.Value >= iProg.MaxVal)
                        {
                            currentCount.Value = 0;
                            inspect = true;
                        }
                    }
                    else if (iProg.RandomFreq > 0)
                    {
                        if (_rand.NextDouble() < iProg.RandomFreq)
                            inspect = true;
                    }

                    //now check lastutc
                    if (iProg.TimeInterval > TimeSpan.Zero &&
                        currentCount.LastUTC != DateTime.MaxValue &&
                        currentCount.LastUTC.Add(iProg.TimeInterval) < utcNow)
                    {
                        inspect = true;
                    }

                    //update lastutc if there is an inspection
                    if (inspect)
                        currentCount.LastUTC = utcNow;

                    //if no lastutc has been recoreded, record the current time.
                    if (currentCount.LastUTC == DateTime.MaxValue)
                        currentCount.LastUTC = utcNow;

                    SetInspectionCount(trans, currentCount);
                }

                if (!alreadyRecorded) {
                    var log = StoreInspectionDecision(trans,
                        matID, uniqueStr, partName, process, actualPath, inspType, counter, utcNow, inspect);
                    logEntries.Add(log);
                }
            }

            return logEntries;
        }

        private MachineWatchInterface.LogEntry StoreInspectionDecision(
            IDbTransaction trans,
            long matID, string unique, string partName, int proc, Dictionary<int, MaterialProcessActualPath> actualPath,
            string inspType, string counter, DateTime utcNow, bool inspect)
        {
            var mat =
                new MachineWatchInterface.LogMaterial(matID, unique, proc, partName, -1);
            var pathSteps = actualPath.Values.OrderBy(p => p.Process).ToList();

            var log = new MachineWatchInterface.LogEntry(
                cntr: -1,
                mat: new MachineWatchInterface.LogMaterial[] {mat},
                pal: "",
                ty: MachineWatchInterface.LogType.Inspection,
                locName: "Inspect",
                locNum: 1,
                prog: counter,
                start: false,
                endTime: utcNow,
                result: inspect.ToString(),
                endOfRoute: false);

            log.ProgramDetails["InspectionType"] = inspType;
            log.ProgramDetails["ActualPath"] = Newtonsoft.Json.JsonConvert.SerializeObject(pathSteps);

            log = AddStationCycle(trans, log, null, null);

            return log;
        }

        #endregion

        #region Force and Next Piece Inspection
        public void ForceInspection(long matID, string inspType)
        {
            var mat = new MachineWatchInterface.LogMaterial(matID, "", 1, "", 1);
            ForceInspection(mat, inspType, inspect: true, utcNow: DateTime.UtcNow);
        }

        public MachineWatchInterface.LogEntry ForceInspection(
            MachineWatchInterface.LogMaterial mat, string inspType, bool inspect)
        {
            return ForceInspection(mat, inspType, inspect, DateTime.UtcNow);
        }

        public MachineWatchInterface.LogEntry ForceInspection(
            MachineWatchInterface.LogMaterial mat, string inspType, bool inspect, DateTime utcNow)
        {
            MachineWatchInterface.LogEntry log;
            lock (_lock)
            {
                var trans = _connection.BeginTransaction();

                try
                {
                    log = RecordForceInspection(trans, mat, inspType, inspect, utcNow);
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }

            if (NewLogEntry != null)
                NewLogEntry(log, null);

            return log;
        }

        private MachineWatchInterface.LogEntry RecordForceInspection(
            IDbTransaction trans,
            MachineWatchInterface.LogMaterial mat, string inspType, bool inspect, DateTime utcNow)
        {
            var log = new MachineWatchInterface.LogEntry(
                cntr: -1,
                mat: new MachineWatchInterface.LogMaterial[] {mat},
                pal: "",
                ty: MachineWatchInterface.LogType.InspectionForce,
                locName: "Inspect",
                locNum: 1,
                prog: inspType,
                start: false,
                endTime: utcNow,
                result: inspect.ToString(),
                endOfRoute: false);
            return AddStationCycle(trans, log, null, null);
        }

        public void NextPieceInspection(MachineWatchInterface.PalletLocation palLoc, string inspType)
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();

                cmd.CommandText = "INSERT OR REPLACE INTO inspection_next_piece(StatType, StatNum, InspType)" +
                    " VALUES ($loc,$locnum,$insp)";
                cmd.Parameters.Add("loc", SqliteType.Integer).Value = (int)palLoc.Location;
                cmd.Parameters.Add("locnum", SqliteType.Integer).Value = palLoc.Num;
                cmd.Parameters.Add("insp", SqliteType.Text).Value = inspType;

                cmd.ExecuteNonQuery();
            }
        }

        public void CheckMaterialForNextPeiceInspection(MachineWatchInterface.PalletLocation palLoc, long matID)
        {
            var logs = new List<MachineWatchInterface.LogEntry>();

            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                var cmd2 = _connection.CreateCommand();

                cmd.CommandText = "SELECT InspType FROM inspection_next_piece WHERE StatType = $loc AND StatNum = $locnum";
                cmd.Parameters.Add("loc", SqliteType.Integer).Value = (int)palLoc.Location;
                cmd.Parameters.Add("locnum", SqliteType.Integer).Value = palLoc.Num;

                var trans = _connection.BeginTransaction();
                try
                {
                    cmd.Transaction = trans;

                    IDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        var now = DateTime.UtcNow;
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                var mat = new MachineWatchInterface.LogMaterial(matID, "", 1, "", 1);
                                logs.Add(RecordForceInspection(trans, mat, reader.GetString(0), inspect:true, utcNow: now));
                            }
                        }

                    }
                    finally
                    {
                        reader.Close();
                    }

                    cmd.CommandText = "DELETE FROM inspection_next_piece WHERE StatType = $loc AND StatNum = $locnum";
                    //keep the same parameters as above
                    cmd.ExecuteNonQuery();

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }

                foreach (var log in logs)
                    NewLogEntry?.Invoke(log, null);
            }
        }
        #endregion
    }
}
