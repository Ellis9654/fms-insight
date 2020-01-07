/* Copyright (c) 2019, John Lenz

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
import { DragDropContext, Droppable, Draggable, DropResult } from "react-beautiful-dnd";
import {
  selectAllMaterialIntoBins,
  MaterialBin,
  MaterialBinType,
  moveMaterialBin,
  MaterialBinId
} from "../../data/all-material-bins";
import { MaterialSummary } from "../../data/events.matsummary";
import { connect, Store, AppActionBeforeMiddleware, mkAC, DispatchAction } from "../../store/store";
import * as matDetails from "../../data/material-details";
import * as currentSt from "../../data/current-status";
import * as guiState from "../../data/gui-state";
import { createSelector } from "reselect";
import {
  Paper,
  Typography,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  DialogContentText
} from "@material-ui/core";
import { LazySeq } from "../../data/lazyseq";
import { InProcMaterial, MaterialDialog } from "../station-monitor/Material";
import { IInProcessMaterial } from "../../data/api";
import { HashMap, Ordering } from "prelude-ts";
// eslint-disable-next-line @typescript-eslint/no-var-requires
const DocumentTitle = require("react-document-title"); // https://github.com/gaearon/react-document-title/issues/58

enum DragType {
  Material = "DRAG_MATERIAL",
  Queue = "DRAG_QUEUE"
}

function getQueueStyle(isDraggingOver: boolean, draggingFromThisWith: string | undefined): React.CSSProperties {
  return {
    display: "flex",
    flexDirection: "column",
    flexWrap: "nowrap",
    width: "18em",
    minHeight: "20em",
    backgroundColor: isDraggingOver ? "#BDBDBD" : draggingFromThisWith ? "#EEEEEE" : undefined
  };
}

interface QuarantineQueueProps {
  readonly queue: string;
  readonly idx: number;
  readonly material: ReadonlyArray<Readonly<IInProcessMaterial>>;
  readonly openMat: (mat: MaterialSummary) => void;
}

const MaterialQueue = React.memo(function DraggableQuarantineQueueF(props: QuarantineQueueProps) {
  return (
    <Draggable draggableId={props.queue} index={props.idx} type={DragType.Queue}>
      {(provided, snapshot) => (
        <Paper
          ref={provided.innerRef}
          {...provided.draggableProps}
          style={{ ...provided.draggableProps.style, margin: "0.75em" }}
        >
          <div {...provided.dragHandleProps}>
            <Typography
              variant="h4"
              {...provided.dragHandleProps}
              color={snapshot.isDragging ? "primary" : "textPrimary"}
            >
              {props.queue}
            </Typography>
          </div>
          <Droppable droppableId={props.queue} type={DragType.Material}>
            {(provided, snapshot) => (
              <div
                ref={provided.innerRef}
                style={getQueueStyle(snapshot.isDraggingOver, snapshot.draggingFromThisWith)}
              >
                {props.material.map((mat, idx) => (
                  <Draggable
                    key={mat.materialID}
                    draggableId={mat.materialID.toString()}
                    index={idx}
                    type={DragType.Material}
                  >
                    {(provided, snapshot) => (
                      <InProcMaterial
                        mat={mat}
                        onOpen={props.openMat}
                        draggableProvided={provided}
                        hideAvatar
                        showDragHandle
                        isDragging={snapshot.isDragging}
                      />
                    )}
                  </Draggable>
                ))}
                {provided.placeholder}
              </div>
            )}
          </Droppable>
        </Paper>
      )}
    </Draggable>
  );
});

interface ActiveQueuesProps {
  readonly draggableId: string;
  readonly idx: number;
  readonly material: HashMap<string, ReadonlyArray<Readonly<IInProcessMaterial>>>;
  readonly openMat: (mat: MaterialSummary) => void;
}

const ActiveQueues = React.memo(function ActiveQueuesF(props: ActiveQueuesProps) {
  return (
    <Draggable draggableId={props.draggableId} index={props.idx} type={DragType.Queue}>
      {(provided, snapshot) => (
        <Paper
          ref={provided.innerRef}
          {...provided.draggableProps}
          style={{ ...provided.draggableProps.style, margin: "0.75em" }}
        >
          <div {...provided.dragHandleProps}>
            <Typography
              variant="h4"
              {...provided.dragHandleProps}
              color={snapshot.isDragging ? "primary" : "textPrimary"}
            >
              Active Queues
            </Typography>
          </div>
          <div style={getQueueStyle(false, undefined)}>
            {LazySeq.ofIterable(props.material)
              .sortBy(([q1, _m1], [q2, _m2]) => q1.localeCompare(q2))
              .map(([queue, material], idx) => (
                <div key={idx}>
                  <Typography variant="caption">{queue}</Typography>
                  <Droppable droppableId={queue} type={DragType.Material}>
                    {(provided, snapshot) => (
                      <div
                        ref={provided.innerRef}
                        style={{
                          ...getQueueStyle(snapshot.isDraggingOver, snapshot.draggingFromThisWith),
                          minHeight: "5em"
                        }}
                      >
                        {material.map((mat, idx) => (
                          <Draggable
                            key={mat.materialID}
                            draggableId={mat.materialID.toString()}
                            isDragDisabled={true}
                            index={idx}
                            type={DragType.Material}
                          >
                            {(provided, snapshot) => (
                              <InProcMaterial
                                draggableProvided={provided}
                                mat={mat}
                                onOpen={props.openMat}
                                hideAvatar
                              />
                            )}
                          </Draggable>
                        ))}
                        {provided.placeholder}
                      </div>
                    )}
                  </Droppable>
                </div>
              ))}
          </div>
        </Paper>
      )}
    </Draggable>
  );
});

interface SystemMaterialProps<T> {
  readonly name: string;
  readonly draggableId: string;
  readonly idx: number;
  readonly material: HashMap<T, ReadonlyArray<Readonly<IInProcessMaterial>>>;
  readonly renderLabel: (label: T) => string;
  readonly compareLabel: (l1: T, l2: T) => Ordering;
  readonly openMat: (mat: MaterialSummary) => void;
}

function renderLul(lul: number) {
  return "L/U " + lul.toString();
}

function compareLul(l1: number, l2: number) {
  return l1 - l2;
}

function renderPal(pal: string) {
  return "Pallet " + pal;
}

function comparePal(p1: string, p2: string) {
  const n1 = parseInt(p1);
  const n2 = parseInt(p2);
  if (isNaN(n1) || isNaN(n2)) {
    return p1.localeCompare(p2);
  } else {
    return n1 - n2;
  }
}

class SystemMaterial<T extends string | number> extends React.PureComponent<SystemMaterialProps<T>> {
  render() {
    return (
      <Draggable draggableId={this.props.draggableId} index={this.props.idx} type={DragType.Queue}>
        {(provided, snapshot) => (
          <Paper
            ref={provided.innerRef}
            {...provided.draggableProps}
            style={{ ...provided.draggableProps.style, margin: "0.75em" }}
          >
            <div {...provided.dragHandleProps}>
              <Typography
                variant="h4"
                {...provided.dragHandleProps}
                color={snapshot.isDragging ? "primary" : "textPrimary"}
              >
                {this.props.name}
              </Typography>
            </div>
            <div style={getQueueStyle(false, undefined)}>
              {LazySeq.ofIterable(this.props.material)
                .sortBy(([l1, _m1], [l2, _m2]) => this.props.compareLabel(l1, l2))
                .map(([label, material], idx) => (
                  <div key={idx}>
                    <Typography variant="caption">{this.props.renderLabel(label)}</Typography>
                    {material.map((mat, idx) => (
                      <InProcMaterial key={idx} mat={mat} onOpen={this.props.openMat} hideAvatar />
                    ))}
                  </div>
                ))}
            </div>
          </Paper>
        )}
      </Draggable>
    );
  }
}

interface AllMatDialogProps {
  readonly display_material: matDetails.MaterialDetail | null;
  readonly quarantineQueue: boolean;
  readonly removeFromQueue: (mat: matDetails.MaterialDetail) => void;
  readonly onClose: () => void;
}

function AllMatDialog(props: AllMatDialogProps) {
  const displayMat = props.display_material;
  return (
    <MaterialDialog
      display_material={props.display_material}
      onClose={props.onClose}
      allowNote={props.quarantineQueue}
      buttons={
        <>
          {displayMat && props.quarantineQueue ? (
            <Button color="primary" onClick={() => props.removeFromQueue(displayMat)}>
              Remove From System
            </Button>
          ) : (
            undefined
          )}
        </>
      }
    />
  );
}

const ConnectedAllMatDialog = connect(st => ({}), {
  onClose: mkAC(matDetails.ActionType.CloseMaterialDialog),
  removeFromQueue: (mat: matDetails.MaterialDetail) =>
    [
      matDetails.removeFromQueue(mat),
      { type: matDetails.ActionType.CloseMaterialDialog },
      { type: guiState.ActionType.SetAddMatToQueueName, queue: undefined }
    ] as AppActionBeforeMiddleware
})(AllMatDialog);

interface ConfirmMoveToActive {
  readonly destQueue: string;
  readonly destQueuePos: number;
  readonly sourceQueue: string;
  readonly sourceQueuePos: number;
  readonly materialId: number;
  readonly serial: string;
}

interface ConfirmMoveActiveDialogProps {
  readonly move: ConfirmMoveToActive | null;
  readonly moveMaterialInQueue: (d: matDetails.AddExistingMaterialToQueueData) => void;
  readonly setMoveActive: (m: ConfirmMoveToActive | null) => void;
  readonly reorderMatInQueue: DispatchAction<currentSt.ActionType.ReorderQueuedMaterial>;
}

function ConfirmMoveActiveDialog(props: ConfirmMoveActiveDialogProps) {
  function cancel() {
    if (props.move) {
      props.reorderMatInQueue({
        materialId: props.move.materialId,
        queue: props.move.sourceQueue,
        newIdx: props.move.sourceQueuePos
      });
      props.setMoveActive(null);
    }
  }
  function confirmMove() {
    if (props.move) {
      props.moveMaterialInQueue({
        materialId: props.move.materialId,
        queue: props.move.destQueue,
        queuePosition: props.move.destQueuePos
      });
      props.setMoveActive(null);
    }
  }
  return (
    <Dialog open={props.move !== null} onClose={cancel}>
      <DialogTitle>Move to {props.move?.destQueue}</DialogTitle>
      <DialogContent>
        <DialogContentText>
          Moving the material with serial {props.move?.serial} back into the active queue {props.move?.destQueue} will
          make it immediately available for machining operations. Are you sure?
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button color="primary" onClick={confirmMove}>
          Move to {props.move?.destQueue}
        </Button>
        <Button color="primary" onClick={cancel}>
          Cancel
        </Button>
      </DialogActions>
    </Dialog>
  );
}

interface AllMaterialProps {
  readonly displaySystemBins: boolean;
  readonly allBins: ReadonlyArray<MaterialBin>;
  readonly display_material: matDetails.MaterialDetail | null;
  readonly openMat: (mat: MaterialSummary) => void;
  readonly moveMaterialInQueue: (d: matDetails.AddExistingMaterialToQueueData) => void;
  readonly moveMaterialBin: (curBinOrder: ReadonlyArray<MaterialBinId>, oldIdx: number, newIdx: number) => void;
  readonly reorderMatInQueue: DispatchAction<currentSt.ActionType.ReorderQueuedMaterial>;
}

function AllMaterial(props: AllMaterialProps) {
  const [confirmMove, setConfirmMove] = React.useState<ConfirmMoveToActive | null>(null);

  const curBins = props.displaySystemBins
    ? props.allBins
    : props.allBins.filter(
        bin => bin.type === MaterialBinType.QuarantineQueues || bin.type === MaterialBinType.ActiveQueues
      );

  const onDragEnd = (result: DropResult): void => {
    if (!result.destination) return;
    if (result.reason === "CANCEL") return;

    if (result.type === DragType.Material) {
      const queue = result.destination.droppableId;
      const materialId = parseInt(result.draggableId);
      const queuePosition = result.destination.index;
      if (curBins.findIndex(bin => bin.type === MaterialBinType.ActiveQueues && bin.byQueue.containsKey(queue)) >= 0) {
        // confirm first.  Visual only reorder and open dialog
        props.reorderMatInQueue({
          materialId: materialId,
          queue: queue,
          newIdx: queuePosition
        });
        let material: Readonly<IInProcessMaterial> | undefined = undefined;
        for (const bin of curBins) {
          if (bin.type === MaterialBinType.QuarantineQueues) {
            material = bin.material.find(m => m.materialID === materialId);
            if (material) {
              break;
            }
          }
        }
        setConfirmMove({
          materialId: materialId,
          destQueue: queue,
          destQueuePos: queuePosition,
          sourceQueue: result.source.droppableId,
          sourceQueuePos: result.source.index,
          serial: material?.serial || ""
        });
      } else {
        props.moveMaterialInQueue({ materialId, queue, queuePosition });
      }
    } else if (result.type === DragType.Queue) {
      props.moveMaterialBin(
        curBins.map(b => b.binId),
        result.source.index,
        result.destination.index
      );
    }
  };

  const curDisplayQuarantine =
    props.display_material !== null &&
    curBins.findIndex(
      bin =>
        bin.type === MaterialBinType.QuarantineQueues &&
        bin.material.findIndex(mat => mat.materialID === props.display_material?.materialID) >= 0
    ) >= 0;

  return (
    <DocumentTitle title="All Material - FMS Insight">
      <DragDropContext onDragEnd={onDragEnd}>
        <Droppable droppableId="Board" type={DragType.Queue} direction="horizontal">
          {provided => (
            <div ref={provided.innerRef} style={{ display: "flex", flexWrap: "nowrap" }}>
              {curBins.map((matBin, idx) => {
                switch (matBin.type) {
                  case MaterialBinType.LoadStations:
                    return (
                      <SystemMaterial
                        name="Load Stations"
                        draggableId={matBin.binId}
                        key={matBin.binId}
                        idx={idx}
                        renderLabel={renderLul}
                        compareLabel={compareLul}
                        material={matBin.byLul}
                        openMat={props.openMat}
                      />
                    );
                  case MaterialBinType.Pallets:
                    return (
                      <SystemMaterial
                        name="Pallets"
                        draggableId={matBin.binId}
                        key={matBin.binId}
                        idx={idx}
                        renderLabel={renderPal}
                        compareLabel={comparePal}
                        material={matBin.byPallet}
                        openMat={props.openMat}
                      />
                    );
                  case MaterialBinType.ActiveQueues:
                    return (
                      <ActiveQueues
                        draggableId={matBin.binId}
                        key={matBin.binId}
                        idx={idx}
                        material={matBin.byQueue}
                        openMat={props.openMat}
                      />
                    );
                  case MaterialBinType.QuarantineQueues:
                    return (
                      <MaterialQueue
                        key={matBin.binId}
                        idx={idx}
                        queue={matBin.queueName}
                        material={matBin.material}
                        openMat={props.openMat}
                      />
                    );
                }
              })}
              {provided.placeholder}
            </div>
          )}
        </Droppable>
        <ConnectedAllMatDialog display_material={props.display_material} quarantineQueue={curDisplayQuarantine} />
        <ConfirmMoveActiveDialog
          move={confirmMove}
          setMoveActive={setConfirmMove}
          moveMaterialInQueue={props.moveMaterialInQueue}
          reorderMatInQueue={props.reorderMatInQueue}
        />
      </DragDropContext>
    </DocumentTitle>
  );
}

const extractMaterialRegions = createSelector(
  (st: Store) => st.Current.current_status,
  (st: Store) => st.AllMatBins.curBinOrder,
  selectAllMaterialIntoBins
);

export default connect(
  st => ({
    allBins: extractMaterialRegions(st),
    display_material: st.MaterialDetails.material
  }),
  {
    openMat: matDetails.openMaterialDialog,
    moveMaterialInQueue: (d: matDetails.AddExistingMaterialToQueueData) => [
      {
        type: currentSt.ActionType.ReorderQueuedMaterial,
        queue: d.queue,
        materialId: d.materialId,
        newIdx: d.queuePosition
      },
      matDetails.addExistingMaterialToQueue(d)
    ],
    reorderMatInQueue: mkAC(currentSt.ActionType.ReorderQueuedMaterial),
    moveMaterialBin: moveMaterialBin
  }
)(AllMaterial);
