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
import * as React from "react";
import AppBar from "@material-ui/core/AppBar";
import Tabs from "@material-ui/core/Tabs";
import Tab from "@material-ui/core/Tab";
import Typography from "@material-ui/core/Typography";
import Toolbar from "@material-ui/core/Toolbar";
import Hidden from "@material-ui/core/Hidden";
import HelpOutline from "@material-ui/icons/HelpOutline";
import ExitToApp from "@material-ui/icons/ExitToApp";
import IconButton from "@material-ui/core/IconButton";
import Tooltip from "@material-ui/core/Tooltip";
import CircularProgress from "@material-ui/core/CircularProgress";
import Button from "@material-ui/core/Button";
import Badge from "@material-ui/core/Badge";
import Notifications from "@material-ui/icons/Notifications";
import { User } from "oidc-client";

import Dashboard from "./dashboard/Dashboard";
import CostPerPiece from "./cost-per-piece/CostPerPiece";
import Efficiency from "./efficiency/Efficiency";
import StationMonitor from "./station-monitor/StationMonitor";
import DataExport from "./data-export/DataExport";
import LoadingIcon from "./LoadingIcon";
import * as routes from "../data/routes";
import { Store, connect, mkAC } from "../store/store";
import * as api from "../data/api";
import * as serverSettings from "../data/server-settings";
import logo from "../seedtactics-logo.svg";
import { DemoMode } from "../data/backend";

const tabsStyle = {
  alignSelf: "flex-end" as "flex-end",
  flexGrow: 1
};

enum TabType {
  Dashboard,
  StationMonitor,
  Efficiency,
  CostPerPiece,
  DataExport
}

interface HeaderProps {
  routeState: routes.State;
  fmsInfo: Readonly<api.IFMSInfo> | null;
  latestVersion: serverSettings.LatestInstaller | null;
  showTabs: boolean;
  alarms: ReadonlyArray<string> | null;
  setRoute: (arg: { ty: TabType; curSt: routes.State }) => void;
  showLogout: boolean;
  onLogout: () => void;
}

function Header(p: HeaderProps) {
  let tabType: TabType = TabType.Dashboard;
  let helpUrl = "https://fms-insight.seedtactics.com/docs/client-dashboard.html";
  switch (p.routeState.current) {
    case routes.RouteLocation.Dashboard:
      tabType = TabType.Dashboard;
      break;
    case routes.RouteLocation.LoadMonitor:
    case routes.RouteLocation.InspectionMonitor:
    case routes.RouteLocation.WashMonitor:
    case routes.RouteLocation.Queues:
    case routes.RouteLocation.AllMaterial:
      tabType = TabType.StationMonitor;
      helpUrl = "https://fms-insight.seedtactics.com/docs/client-station-monitor.html";
      break;
    case routes.RouteLocation.Efficiency:
      tabType = TabType.Efficiency;
      helpUrl = "https://fms-insight.seedtactics.com/docs/client-efficiency.html";
      break;
    case routes.RouteLocation.CostPerPiece:
      tabType = TabType.CostPerPiece;
      helpUrl = "https://fms-insight.seedtactics.com/docs/client-cost-per-piece.html";
      break;
    case routes.RouteLocation.DataExport:
      tabType = TabType.DataExport;
  }
  const tabs = (full: boolean) => (
    <Tabs
      fullWidth={full}
      style={full ? {} : tabsStyle}
      value={tabType}
      onChange={(e, v) => p.setRoute({ ty: v, curSt: p.routeState })}
    >
      <Tab label="Dashboard" value={TabType.Dashboard} />
      <Tab label="Station Monitor" value={TabType.StationMonitor} />
      <Tab label="Efficiency" value={TabType.Efficiency} />
      <Tab label="Cost/Piece" value={TabType.CostPerPiece} />
      {DemoMode ? undefined : <Tab label="Data Export" value={TabType.DataExport} />}
    </Tabs>
  );

  const alarmTooltip = p.alarms ? p.alarms.join(". ") : "No Alarms";
  const Alarms = () => (
    <Tooltip title={alarmTooltip}>
      <Badge badgeContent={p.alarms ? p.alarms.length : 0}>
        <Notifications color={p.alarms ? "error" : undefined} />
      </Badge>
    </Tooltip>
  );

  const HelpButton = () => (
    <Tooltip title="Help">
      <IconButton aria-label="Help" href={helpUrl} target="_help">
        <HelpOutline />
      </IconButton>
    </Tooltip>
  );

  const LogoutButton = () => (
    <Tooltip title="Logout">
      <IconButton aria-label="Logout" onClick={p.onLogout}>
        <ExitToApp />
      </IconButton>
    </Tooltip>
  );

  let tooltip: JSX.Element | string = "";
  if (p.fmsInfo && p.latestVersion) {
    tooltip = (
      <div>
        {(p.fmsInfo.name || "") + " " + (p.fmsInfo.version || "")}
        <br />
        Latest version {p.latestVersion.version} ({p.latestVersion.date.toDateString()})
      </div>
    );
  } else if (p.fmsInfo) {
    tooltip = (p.fmsInfo.name || "") + " " + (p.fmsInfo.version || "");
  }

  const largeAppBar = (
    <AppBar position="static">
      <Toolbar>
        {DemoMode ? (
          <a href="/">
            <img src={logo} alt="Logo" style={{ height: "30px", marginRight: "1em" }} />
          </a>
        ) : (
          <Tooltip title={tooltip}>
            <img src={logo} alt="Logo" style={{ height: "30px", marginRight: "1em" }} />
          </Tooltip>
        )}
        <Typography variant="h6" style={{ marginRight: "2em" }}>
          Insight
        </Typography>
        {p.showTabs ? tabs(false) : <div style={{ flexGrow: 1 }} />}
        <LoadingIcon />
        <Alarms />
        <HelpButton />
        {p.showLogout ? <LogoutButton /> : undefined}
      </Toolbar>
    </AppBar>
  );

  const smallAppBar = (
    <AppBar position="static">
      <Toolbar>
        <Tooltip title={tooltip}>
          <img src="/seedtactics-logo.svg" alt="Logo" style={{ height: "25px", marginRight: "4px" }} />
        </Tooltip>
        <Typography variant="h6">Insight</Typography>
        <div style={{ flexGrow: 1 }} />
        <LoadingIcon />
        <Alarms />
        <HelpButton />
        {p.showLogout ? <LogoutButton /> : undefined}
      </Toolbar>
      {p.showTabs ? tabs(true) : undefined}
    </AppBar>
  );

  return (
    <nav id="navHeader">
      <Hidden smDown>{largeAppBar}</Hidden>
      <Hidden mdUp>{smallAppBar}</Hidden>
    </nav>
  );
}

interface AppProps {
  route: routes.State;
  fmsInfo: Readonly<api.IFMSInfo> | null;
  user: User | null;
  latestVersion: serverSettings.LatestInstaller | null;
  alarms: ReadonlyArray<string> | null;
  setRoute: (arg: { ty: TabType; curSt: routes.State }) => void;
  onLogin: () => void;
  onLogout: () => void;
}

class App extends React.PureComponent<AppProps> {
  render() {
    let page: JSX.Element;
    let showTabs: boolean = true;
    let showLogout: boolean = !!this.props.user;
    if (this.props.fmsInfo && (!this.props.fmsInfo.openIDConnectAuthority || this.props.user)) {
      switch (this.props.route.current) {
        case routes.RouteLocation.CostPerPiece:
          page = <CostPerPiece />;
          break;
        case routes.RouteLocation.Efficiency:
          page = <Efficiency />;
          break;
        case routes.RouteLocation.LoadMonitor:
          page = <StationMonitor monitor_type={routes.StationMonitorType.LoadUnload} />;
          break;
        case routes.RouteLocation.InspectionMonitor:
          page = <StationMonitor monitor_type={routes.StationMonitorType.Inspection} />;
          break;
        case routes.RouteLocation.WashMonitor:
          page = <StationMonitor monitor_type={routes.StationMonitorType.Wash} />;
          break;
        case routes.RouteLocation.Queues:
          page = <StationMonitor monitor_type={routes.StationMonitorType.Queues} />;
          break;
        case routes.RouteLocation.AllMaterial:
          page = <StationMonitor monitor_type={routes.StationMonitorType.AllMaterial} />;
          break;
        case routes.RouteLocation.DataExport:
          page = <DataExport />;
          break;
        case routes.RouteLocation.Dashboard:
        default:
          page = <Dashboard />;
          break;
      }
    } else if (this.props.fmsInfo && this.props.fmsInfo.openIDConnectAuthority) {
      page = (
        <div style={{ textAlign: "center", marginTop: "4em" }}>
          <h3>Please Login</h3>
          <Button variant="contained" color="primary" onClick={this.props.onLogin}>
            Login
          </Button>
        </div>
      );
      showTabs = false;
    } else {
      page = (
        <div style={{ textAlign: "center", marginTop: "4em" }}>
          <CircularProgress />
          <p>Loading</p>
        </div>
      );
      showTabs = false;
    }
    return (
      <div id="App">
        <Header
          routeState={this.props.route}
          fmsInfo={this.props.fmsInfo}
          latestVersion={this.props.latestVersion}
          showTabs={showTabs}
          showLogout={showLogout}
          setRoute={this.props.setRoute}
          onLogout={this.props.onLogout}
          alarms={this.props.alarms}
        />
        {page}
      </div>
    );
  }
}

function emptyToNull<T>(s: ReadonlyArray<T> | null): ReadonlyArray<T> | null {
  if (!s || s.length === 0) {
    return null;
  } else {
    return s;
  }
}

export default connect(
  (s: Store) => ({
    route: s.Route,
    fmsInfo: s.ServerSettings.fmsInfo || null,
    user: s.ServerSettings.user || null,
    latestVersion: s.ServerSettings.latestInstaller || null,
    alarms: emptyToNull(s.Current.current_status.alarms)
  }),
  {
    setRoute: ({ ty, curSt }: { ty: TabType; curSt: routes.State }): routes.Action => {
      switch (ty) {
        case TabType.Dashboard:
          return { type: routes.RouteLocation.Dashboard };
        case TabType.Efficiency:
          return { type: routes.RouteLocation.Efficiency };
        case TabType.CostPerPiece:
          return { type: routes.RouteLocation.CostPerPiece };
        case TabType.StationMonitor:
          return routes.switchToStationMonitorPage(curSt);
        case TabType.DataExport:
          return { type: routes.RouteLocation.DataExport };
      }
    },
    onLogin: mkAC(serverSettings.ActionType.Login),
    onLogout: mkAC(serverSettings.ActionType.Logout)
  }
)(App);
