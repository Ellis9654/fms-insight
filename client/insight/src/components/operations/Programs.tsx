/* Copyright (c) 2020, John Lenz

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
import * as React from "react";
import Fab from "@material-ui/core/Fab";
import CircularProgress from "@material-ui/core/CircularProgress";
import Card from "@material-ui/core/Card";
import CardContent from "@material-ui/core/CardContent";
import TimeAgo from "react-timeago";
import RefreshIcon from "@material-ui/icons/Refresh";
import CardHeader from "@material-ui/core/CardHeader";
import ToolIcon from "@material-ui/icons/Dns";
import Table from "@material-ui/core/Table";
import TableHead from "@material-ui/core/TableHead";
import TableCell from "@material-ui/core/TableCell";
import TableRow from "@material-ui/core/TableRow";
import TableSortLabel from "@material-ui/core/TableSortLabel";
import Tooltip from "@material-ui/core/Tooltip";
import { calcProgramSummary, CellControllerProgram } from "../../data/tools-programs";
import TableBody from "@material-ui/core/TableBody";
import IconButton from "@material-ui/core/IconButton";
import KeyboardArrowDownIcon from "@material-ui/icons/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@material-ui/icons/KeyboardArrowUp";
import Collapse from "@material-ui/core/Collapse";
import { LazySeq } from "../../data/lazyseq";
import { makeStyles } from "@material-ui/core/styles";
import { Store, connect, mkAC, DispatchAction } from "../../store/store";
import { PartIdenticon } from "../station-monitor/Material";
import { useStore } from "react-redux";
import { Vector } from "prelude-ts";

interface ProgramRowProps {
  readonly program: CellControllerProgram;
}

const useRowStyles = makeStyles({
  mainRow: {
    "& > *": {
      borderBottom: "unset",
    },
  },
  collapseCell: {
    paddingBottom: 0,
    paddingTop: 0,
  },
  detailContainer: {
    marginRight: "1em",
    marginLeft: "3em",
  },
  detailTable: {
    width: "auto",
    marginLeft: "10em",
    marginBottom: "1em",
  },
  partNameContainer: {
    display: "flex",
    alignItems: "center",
  },
});

function ProgramRow(props: ProgramRowProps) {
  const [open, setOpen] = React.useState<boolean>(false);
  const classes = useRowStyles();

  return (
    <>
      <TableRow className={classes.mainRow}>
        <TableCell>
          {props.program.toolUse === null || props.program.toolUse.tools.length === 0 ? undefined : (
            <IconButton size="small" onClick={() => setOpen(!open)}>
              {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
            </IconButton>
          )}
        </TableCell>
        <TableCell>{props.program.programName}</TableCell>
        <TableCell>{props.program.cellControllerProgramName}</TableCell>
        <TableCell>
          {props.program.partName !== null ? (
            <div className={classes.partNameContainer}>
              <PartIdenticon part={props.program.partName} size={20} />
              <span>
                {props.program.partName}-{props.program.process}
              </span>
            </div>
          ) : undefined}
        </TableCell>
        <TableCell>{props.program.comment ?? ""}</TableCell>
        <TableCell>{props.program.revision === null ? "" : props.program.revision.toFixed()}</TableCell>
        <TableCell align="right">
          {props.program.statisticalCycleTime === null
            ? ""
            : props.program.statisticalCycleTime.medianMinutesForSingleMat.toFixed(2)}
        </TableCell>
        <TableCell align="right">
          {props.program.statisticalCycleTime === null
            ? ""
            : props.program.statisticalCycleTime.MAD_aboveMinutes.toFixed(2)}
        </TableCell>
        <TableCell align="right">
          {props.program.statisticalCycleTime === null
            ? ""
            : props.program.statisticalCycleTime.MAD_belowMinutes.toFixed(2)}
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell className={classes.collapseCell} colSpan={9}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <div className={classes.detailContainer}>
              {props.program.toolUse === null || props.program.toolUse.tools.length === 0 ? undefined : (
                <Table size="small" className={classes.detailTable}>
                  <TableHead>
                    <TableRow>
                      <TableCell>Tool</TableCell>
                      <TableCell align="right">Estimated Usage (min)</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {LazySeq.ofIterable(props.program.toolUse.tools).map((t, idx) => (
                      <TableRow key={idx}>
                        <TableCell>{t.toolName}</TableCell>
                        <TableCell align="right">{t.cycleUsageMinutes}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </div>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  );
}

interface ProgramTableProps {
  readonly programs: Vector<CellControllerProgram>;
}

type SortColumn =
  | "ProgramName"
  | "CellProgName"
  | "Comment"
  | "Revision"
  | "PartName"
  | "MedianTime"
  | "DeviationAbove"
  | "DeviationBelow";

function ProgramSummaryTable(props: ProgramTableProps) {
  const [sortCol, setSortCol] = React.useState<SortColumn>("ProgramName");
  const [sortDir, setSortDir] = React.useState<"asc" | "desc">("asc");

  const rows = props.programs.sortBy((a: CellControllerProgram, b: CellControllerProgram) => {
    let c: number = 0;
    switch (sortCol) {
      case "ProgramName":
        c = a.programName.localeCompare(b.programName);
        break;
      case "CellProgName":
        c = a.cellControllerProgramName.localeCompare(b.cellControllerProgramName);
        break;
      case "Comment":
        if (a.comment === null && b.comment === null) {
          c = 0;
        } else if (a.comment === null) {
          c = 1;
        } else if (b.comment === null) {
          c = -1;
        } else {
          c = a.comment.localeCompare(b.comment);
        }
        break;
      case "Revision":
        if (a.revision === null && b.revision === null) {
          c = 0;
        } else if (a.revision === null) {
          c = 1;
        } else if (b.revision === null) {
          c = -1;
        } else {
          c = a.revision - b.revision;
        }
        break;
      case "PartName":
        if (a.partName === null && b.partName === null) {
          c = 0;
        } else if (a.partName === null) {
          c = 1;
        } else if (b.partName === null) {
          c = -1;
        } else {
          c = a.partName.localeCompare(b.partName);
          if (c === 0) {
            c = (a.process ?? 1) - (b.process ?? 1);
          }
        }
        break;
      case "MedianTime":
        if (a.statisticalCycleTime === null && b.statisticalCycleTime === null) {
          c = 0;
        } else if (a.statisticalCycleTime === null) {
          c = 1;
        } else if (b.statisticalCycleTime === null) {
          c = -1;
        } else {
          c = a.statisticalCycleTime.medianMinutesForSingleMat - b.statisticalCycleTime.medianMinutesForSingleMat;
        }
        break;
      case "DeviationAbove":
        if (a.statisticalCycleTime === null && b.statisticalCycleTime === null) {
          c = 0;
        } else if (a.statisticalCycleTime === null) {
          c = 1;
        } else if (b.statisticalCycleTime === null) {
          c = -1;
        } else {
          c = a.statisticalCycleTime.MAD_aboveMinutes - b.statisticalCycleTime.MAD_aboveMinutes;
        }
        break;
      case "DeviationBelow":
        if (a.statisticalCycleTime === null && b.statisticalCycleTime === null) {
          c = 0;
        } else if (a.statisticalCycleTime === null) {
          c = 1;
        } else if (b.statisticalCycleTime === null) {
          c = -1;
        } else {
          c = a.statisticalCycleTime.MAD_belowMinutes - b.statisticalCycleTime.MAD_belowMinutes;
        }
        break;
    }
    if (c === 0) {
      return 0;
    } else if ((c < 0 && sortDir === "asc") || (c > 0 && sortDir === "desc")) {
      return -1;
    } else {
      return 1;
    }
  });

  function toggleSort(s: SortColumn) {
    if (s === sortCol) {
      setSortDir(sortDir === "asc" ? "desc" : "asc");
    } else {
      setSortCol(s);
    }
  }

  return (
    <Table>
      <TableHead>
        <TableRow>
          <TableCell />
          <TableCell sortDirection={sortCol === "ProgramName" ? sortDir : false}>
            <TableSortLabel
              active={sortCol === "ProgramName"}
              direction={sortDir}
              onClick={() => toggleSort("ProgramName")}
            >
              Program Name
            </TableSortLabel>
          </TableCell>
          <TableCell sortDirection={sortCol === "CellProgName" ? sortDir : false}>
            <TableSortLabel
              active={sortCol === "CellProgName"}
              direction={sortDir}
              onClick={() => toggleSort("CellProgName")}
            >
              Cell Controller Program
            </TableSortLabel>
          </TableCell>
          <TableCell sortDirection={sortCol === "PartName" ? sortDir : false}>
            <TableSortLabel active={sortCol === "PartName"} direction={sortDir} onClick={() => toggleSort("PartName")}>
              Part
            </TableSortLabel>
          </TableCell>
          <TableCell sortDirection={sortCol === "Comment" ? sortDir : false}>
            <TableSortLabel active={sortCol === "Comment"} direction={sortDir} onClick={() => toggleSort("Comment")}>
              Comment
            </TableSortLabel>
          </TableCell>
          <TableCell sortDirection={sortCol === "Revision" ? sortDir : false}>
            <TableSortLabel active={sortCol === "Revision"} direction={sortDir} onClick={() => toggleSort("Revision")}>
              Revision
            </TableSortLabel>
          </TableCell>
          <TableCell sortDirection={sortCol === "MedianTime" ? sortDir : false} align="right">
            <TableSortLabel
              active={sortCol === "MedianTime"}
              direction={sortDir}
              onClick={() => toggleSort("MedianTime")}
            >
              Median Time / Material (min)
            </TableSortLabel>
          </TableCell>
          <TableCell sortDirection={sortCol === "DeviationAbove" ? sortDir : false} align="right">
            <TableSortLabel
              active={sortCol === "DeviationAbove"}
              direction={sortDir}
              onClick={() => toggleSort("DeviationAbove")}
            >
              Deviation Above Median
            </TableSortLabel>
          </TableCell>
          <TableCell sortDirection={sortCol === "DeviationBelow" ? sortDir : false} align="right">
            <TableSortLabel
              active={sortCol === "DeviationBelow"}
              direction={sortDir}
              onClick={() => toggleSort("DeviationBelow")}
            >
              Deviation Below Median
            </TableSortLabel>
          </TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {LazySeq.ofIterable(rows).map((program, idx) => (
          <ProgramRow key={idx} program={program} />
        ))}
      </TableBody>
    </Table>
  );
}

interface ProgNavHeaderProps {
  readonly refreshTime: Date | null;
  readonly loading: boolean;
  readonly loadPrograms: () => void;
}

function ProgNavHeader(props: ProgNavHeaderProps) {
  if (props.refreshTime === null) {
    return (
      <main style={{ margin: "2em", display: "flex", justifyContent: "center" }}>
        <Fab
          color="secondary"
          size="large"
          variant="extended"
          style={{ margin: "2em" }}
          onClick={props.loadPrograms}
          disabled={props.loading}
        >
          {props.loading ? (
            <>
              <CircularProgress size={10} style={{ marginRight: "1em" }} />
              Loading
            </>
          ) : (
            <>
              <RefreshIcon style={{ marginRight: "1em" }} />
              Load Programs
            </>
          )}
        </Fab>
      </main>
    );
  } else {
    return (
      <nav
        style={{
          display: "flex",
          backgroundColor: "#E0E0E0",
          paddingLeft: "24px",
          paddingRight: "24px",
          minHeight: "2.5em",
          alignItems: "center",
        }}
      >
        <Tooltip title="Refresh Tools">
          <div>
            <IconButton onClick={props.loadPrograms} disabled={props.loading} size="small">
              {props.loading ? <CircularProgress size={10} /> : <RefreshIcon fontSize="inherit" />}
            </IconButton>
          </div>
        </Tooltip>
        <span style={{ marginLeft: "1em" }}>
          Programs from <TimeAgo date={props.refreshTime} />
        </span>
      </nav>
    );
  }
}

interface ProgReportContentProps {
  readonly programs: Vector<CellControllerProgram> | null;
  readonly time: Date | null;
  readonly setReport: DispatchAction<"Programs_SetProgramData">;
}

function ProgReportContent(props: ProgReportContentProps) {
  React.useEffect(() => {
    document.title = "Programs - FMS Insight";
  }, []);
  const [loading, setLoading] = React.useState<boolean>(false);
  const [error, setError] = React.useState<string | null>(null);
  const store = useStore<Store>();

  const loadPrograms = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      props.setReport(await calcProgramSummary(store));
    } catch (e) {
      setError(e);
    } finally {
      setLoading(false);
    }
  }, [setLoading, setError, props.setReport, store]);

  return (
    <>
      <ProgNavHeader loading={loading} loadPrograms={loadPrograms} refreshTime={props.time} />
      <main style={{ padding: "24px" }}>
        {error != null ? (
          <Card>
            <CardContent>{error}</CardContent>
          </Card>
        ) : undefined}
        {props.programs !== null ? (
          <Card raised>
            <CardHeader
              title={
                <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center" }}>
                  <ToolIcon style={{ color: "#6D4C41" }} />
                  <div style={{ marginLeft: "10px", marginRight: "3em" }}>Cell Controller Programs</div>
                </div>
              }
            />
            <CardContent>
              <ProgramSummaryTable programs={props.programs} />
            </CardContent>
          </Card>
        ) : undefined}
      </main>
    </>
  );
}

export const ProgramReportPage = connect(
  (s) => ({
    programs: s.ToolsPrograms.programs,
    time: s.ToolsPrograms.program_time,
  }),
  {
    setReport: mkAC("Programs_SetProgramData"),
  }
)(ProgReportContent);
