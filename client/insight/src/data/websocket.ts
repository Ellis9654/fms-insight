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

import ReconnectingWebSocket from 'reconnecting-websocket';
import * as redux from 'redux';
import * as events from './events';
import { LogEntry } from './api';

export interface State {
  websocket_reconnecting: boolean;
}

export const initial: State = {
  websocket_reconnecting: true
};

export enum ActionType {
  WebsocketOpen = 'Websocket_Open',
  WebsocketClose = 'Websocket_Close',
}

export type Action =
  | {type: ActionType.WebsocketOpen }
  | {type: ActionType.WebsocketClose}
  ;

export function reducer(s: State, a: Action): State {
  if (s === undefined) { return initial; }
  switch (a.type) {
    case ActionType.WebsocketOpen:
      return {websocket_reconnecting: false};
    case ActionType.WebsocketClose:
      return {websocket_reconnecting: true};

    default: return s;
  }
}

export function openWebsocket(d: (a: redux.AnyAction) => void, getEvtState: () => events.State) {
  const loc = window.location;
  let uri = "";
  if (loc.protocol === "https:") {
      uri = "wss:";
  } else {
      uri = "ws:";
  }
  uri += "//" + loc.host + "/api/v1/events";

  const websocket = new ReconnectingWebSocket(uri);
  websocket.onopen = () => {
    d({type: ActionType.WebsocketOpen});

    const st = getEvtState();
    if (st.last30.latest_counter !== undefined) {
      d(events.refreshEvents(st.last30.latest_counter));
    }
  };
  websocket.onclose = () => {
    d({type: ActionType.WebsocketClose});
  };
  websocket.onmessage = (evt) => {
    const entry = LogEntry.fromJS(JSON.parse(evt.data));
    d(events.receiveNewEvents([entry]));
  };
  // websocket.open();
}