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
import * as api from './api';
import { addDays } from 'date-fns';
import * as im from 'immutable'; // consider collectable.js at some point?

export interface MaterialDetails {
  readonly materialID: number;
  readonly jobUnique: string;
  readonly partName: string;
  readonly last_event: Date;
  readonly completed_procs: ReadonlyArray<number>;

  readonly serial?: string;
  readonly workorderId?: string;

  readonly completed_time?: Date;

  readonly wash_completed?: Date;

  readonly signaledInspections: ReadonlyArray<string>;
  readonly completedInspections: ReadonlyArray<string>;
}

export interface MatDetailState {
  readonly matsById: im.Map<number, MaterialDetails>;
}

export const initial: MatDetailState = {
  matsById: im.Map(),
};

export function process_events(now: Date, newEvts: Iterable<api.ILogEntry>, st: MatDetailState): MatDetailState {
  const oneWeekAgo = addDays(now, -7);

  const evtsSeq = im.Seq(newEvts);

  // check if no changes needed: no new events and nothing to filter out
  const minEntry = st.matsById.valueSeq().minBy(m => m.last_event);
  if ((minEntry === undefined || minEntry.last_event >= oneWeekAgo) && evtsSeq.isEmpty()) {
      return st;
  }

  let mats = st.matsById.filter(e => e.last_event >= oneWeekAgo);

  evtsSeq
    .filter(e => !e.startofcycle && e.material.length > 0)
    .forEach(e => {
    for (let logMat of e.material) {
      let mat = mats.get(logMat.id);
      if (mat) {
        mat = {...mat, last_event: e.endUTC};
      } else {
        mat = {
          materialID: logMat.id,
          jobUnique: logMat.uniq,
          partName: logMat.part,
          last_event: e.endUTC,
          completed_procs: [],
          signaledInspections: [],
          completedInspections: [],
        };
      }

      switch (e.type) {
        case api.LogType.PartMark:
          mat = {...mat, serial: e.result};
          break;

        case api.LogType.OrderAssignment:
          mat = {...mat, workorderId: e.result};
          break;

        case api.LogType.Inspection:
          if (e.result.toLowerCase() === "true" || e.result === "1") {
            const entries = e.program.split(",");
            if (entries.length >= 2) {
              mat = {...mat, signaledInspections: [...mat.signaledInspections, entries[1]]};
            }
          }
          break;

        case api.LogType.InspectionResult:
          mat = {...mat, completedInspections: [...mat.completedInspections, e.program]};
          break;

        case api.LogType.LoadUnloadCycle:
          if (e.result === "UNLOAD") {
            if (logMat.proc === logMat.numproc) {
              mat = {...mat,
                completed_procs: [...mat.completed_procs, logMat.proc],
                completed_time: e.endUTC
              };
            } else {
              mat = {...mat,
                completed_procs: [...mat.completed_procs, logMat.proc],
              };
            }
          }
          break;

        case api.LogType.Wash:
          mat = {...mat, wash_completed: e.endUTC};
          break;
      }

      mats.set(logMat.id, mat);
    }
  });

  return {...st, matsById: mats};
}