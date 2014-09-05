using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.IO;

/******************************************************************************
 * Copyright (c) 2013-2014, Justin Bengtson
 * Copyright (c) 2014, Maik Schreiber
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met: 
 * 
 * 1. Redistributions of source code must retain the above copyright notice,
 * this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 * this list of conditions and the following disclaimer in the documentation
 * and/or other materials provided with the distribution.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 ******************************************************************************/

namespace RegexKSP {
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class PreciseNode : MonoBehaviour {
		private enum Key {
			NONE,
			PROGINC,
			PROGDEC,
			NORMINC,
			NORMDEC,
			RADIINC,
			RADIDEC,
			TIMEINC,
			TIMEDEC,
			PAGEINC,
			PAGECON,
			HIDEWINDOW,
			ADDWIDGET
		};

		internal static int VERSION = 6;

		private static readonly Color PROGRADE_COLOR = new Color(86, 144, 0);
		private static readonly Color NORMAL_COLOR = new Color(151, 0, 162);
		private static readonly Color RADIAL_COLOR = new Color(0, 136, 130);

		internal PluginConfiguration config;

		private PreciseNodeOptions options = new PreciseNodeOptions();
		private NodeManager curState = new NodeManager();
		private List<Action> scheduledForLayout = new List<Action>();

		private bool configLoaded;
		private bool conicsLoaded;
		private bool shown = true;
		private bool showTimeNext;
		private bool waitForKey;
		private bool showOptions;
		private bool showKeymapper;
		private bool showEncounter;
		private Key currentWaitKey = Key.NONE;
		private double keyWaitTime = 0.0;

		private readonly int mainWindowId = WindowId.GetNext();
		private readonly int optionsWindowId = WindowId.GetNext();
		private readonly int keymapperWindowId = WindowId.GetNext();
		private readonly int tripWindowId = WindowId.GetNext();
		private readonly int clockWindowId = WindowId.GetNext();
		private readonly int conicsWindowId = WindowId.GetNext();

		/// <summary>
		/// Overridden function from MonoBehavior
		/// </summary>
		internal void Awake() {
			CancelInvoke();
			loadConfig();
		}

		/// <summary>
		/// Overridden function from MonoBehavior
		/// </summary>
		internal void OnDisable() {
			saveConfig();
		}

		/// <summary>
		/// Overridden function from MonoBehavior
		/// </summary>
		internal void Update() {
			if(!FlightDriver.Pause && canShowNodeEditor) {
                PatchedConicSolver solver = NodeTools.getSolver();
                if(solver.maneuverNodes.Count > 0) {
                    if(!curState.hasNode() || !solver.maneuverNodes.Contains(curState.node)) {
                        // get the first one if we can't find the current or it's null
                        curState = new NodeManager(solver.maneuverNodes[0]);
                    } else if(curState.hasNode()) {
                        curState.updateNode();
                        curState = curState.nextState();
                    }
                } else {
                    if(curState.hasNode()) {
                        curState = new NodeManager();
                        curState.resizeClockWindow = true;
                    }
                }
                processKeyInput();
			}
		}

#if NODE_CLEANUP
        internal void FixedUpdate() {
            if(!FlightDriver.Pause) {
                PatchedConicSolver solver = NodeTools.getSolver();
                if(options.removeUsedNodes && solver.maneuverNodes.Count > 0) {
                    ManeuverNode node = solver.maneuverNodes[0];
                    if(node.GetBurnVector(FlightGlobals.ActiveVessel.orbit).magnitude < options.usedNodeThreshold) {
                        solver.RemoveManeuverNode(node);
                        //TODO: Clean up states after removing the node.
                    }
                }
            }
        }
#endif

		/// <summary>
		/// Overridden function from MonoBehavior
		/// </summary>
		internal void OnGUI() {
            GUIParts.initGUI(options.useKspSkin);
			// Porcess any scheduled functions
			if(Event.current.type == EventType.Layout && !FlightDriver.Pause && scheduledForLayout.Count > 0) {
				foreach(Action a in scheduledForLayout) {
					a();
				}
				scheduledForLayout.Clear();
			}
			if(canShowNodeEditor) {
				if(Event.current.type == EventType.Layout && !FlightDriver.Pause) {
					// On layout we should see if we have nodes to act on.
					if(curState.resizeMainWindow) {
						options.mainWindowPos.height = 0;
                        options.mainWindowPos.width  = 0; //DaMichel: reset width as well because a change in style needs contraction in width.
					}
					if(curState.resizeClockWindow) {
						options.clockWindowPos.height = 0;
                        options.clockWindowPos.width = 0;
					}
					//DaMichel: resize all windows when the gui style changes
					if(curState.resizeOtherWindows) {
						options.optionsWindowPos.height = 0;
						options.keymapperWindowPos.height = 0;
						options.conicsWindowPos.height = 0;
						options.tripWindowPos.height = 0;
						options.optionsWindowPos.width = 0;
						options.keymapperWindowPos.width = 0;
						options.conicsWindowPos.width = 0;
						options.tripWindowPos.width = 0;
					}
					showEncounter = curState.encounter;
					// this prevents the clock window from showing the time to
					// next node when the next state is created during repaint.
					showTimeNext = curState.hasNode();
				}
				if(!conicsLoaded) {
					NodeTools.changeConicsMode(options.conicsMode);
					conicsLoaded = true;
				}
				if(shown) {
					drawGUI();
				}
			}
			if (canShowClock) {
				drawClockGUI();
			}
			if (canShowConicsWindow) {
				drawConicsGUI();
			}
		}

		/// <summary>
		/// Draw Node Editor and Options GUI
		/// </summary>
		private void drawGUI() {
			options.mainWindowPos = GUILayout.Window(mainWindowId, options.mainWindowPos, (id) => drawMainWindow(),
				"Precise Node", GUILayout.ExpandHeight(true));
			if(showOptions) {
				//DaMichel: added hardcoded minimal width. See GUIParts.
				options.optionsWindowPos = GUILayout.Window(optionsWindowId, options.optionsWindowPos, (id) => drawOptionsWindow(),
					"Precise Node Options", GUILayout.ExpandHeight(true), GUILayout.MinWidth(GUIParts.optionsWindowSize));
			}
			if(showKeymapper) {
				options.keymapperWindowPos = GUILayout.Window(keymapperWindowId, options.keymapperWindowPos, (id) => drawKeymapperWindow(),
					"Precise Node Keys", GUILayout.ExpandHeight(true));
			}
			if(options.showTrip) {
				options.tripWindowPos = GUILayout.Window(tripWindowId, options.tripWindowPos, (id) => drawTripWindow(),
					"Trip Info", GUILayout.ExpandHeight(true));
			}
		}

		/// <summary>
		/// Draw Clock GUI
		/// </summary>
		private void drawClockGUI() {
			options.clockWindowPos = GUILayout.Window(clockWindowId, options.clockWindowPos, (id) => drawClockWindow(),
				"Clock", GUILayout.ExpandHeight(true));
		}

		/// <summary>
		/// Draw Conics GUI
		/// </summary>
		private void drawConicsGUI() {
			options.conicsWindowPos = GUILayout.Window(conicsWindowId, options.conicsWindowPos, (id) => drawConicsWindow(),
				"Conics Controls", GUILayout.ExpandHeight(true));
		}

		/// <summary>
		/// Draws the Node Editor window.
		/// </summary>
		private void drawMainWindow() {
			Color defaultColor = GUI.backgroundColor;
			Color contentColor = GUI.contentColor;
			Color curColor = defaultColor;
			PatchedConicSolver solver = NodeTools.getSolver();

			// Options button
			if(showOptions) { GUI.backgroundColor = Color.green; }
			if(GUI.Button(new Rect(options.mainWindowPos.width - 48, 2, 22, 18), "O")) {
				showOptions = !showOptions;
			}
			GUI.backgroundColor = defaultColor;

			// Keymapping button
			if(showKeymapper) { GUI.backgroundColor = Color.green; }
			if(GUI.Button(new Rect(options.mainWindowPos.width - 24, 2, 22, 18), "K")) {
				showKeymapper = !showKeymapper;
			}
			GUI.backgroundColor = defaultColor;

			GUILayout.BeginVertical();
				GUILayout.BeginVertical(GUIParts.styleFrameless);
					if(options.showManeuverPager) {
						drawManeuverPager();
					}
					// Human-readable time
					GUIParts.drawDoubleLabel("Time:", GUIParts.leftColumnWidth, curState.currentUT().convertUTtoHumanTime(), 150);
				GUILayout.EndVertical();

				GUILayout.BeginVertical(GUIParts.styleFramed);
					// Increment buttons
					GUILayout.BeginHorizontal();
						GUILayout.Label("Increment:", GUILayout.Width(GUIParts.leftColumnNodeWidth));
						GUIParts.drawButton("0.01", (options.increment == 0.01?Color.yellow:defaultColor), () => { options.increment = 0.01; });
						GUIParts.drawButton("0.1", (options.increment == 0.1?Color.yellow:defaultColor), () => { options.increment = 0.1; });
						GUIParts.drawButton("1", (options.increment == 1?Color.yellow:defaultColor), () => { options.increment = 1; });
						GUIParts.drawButton("10", (options.increment == 10?Color.yellow:defaultColor), () => { options.increment = 10; });
						GUIParts.drawButton("100", (options.increment == 100?Color.yellow:defaultColor), () => { options.increment = 100; });
					GUILayout.EndHorizontal();

					drawTimeControls(contentColor);

					GUILayout.BeginHorizontal();
						GUILayout.BeginVertical();
							drawProgradeControls(contentColor);
							drawNormalControls(contentColor);
							drawRadialControls(contentColor);
						GUILayout.EndVertical();
						GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
							GUIParts.drawButton("MS", defaultColor, () => {
								curState.memorize();
							}, GUILayout.ExpandHeight(true));
							GUI.enabled = curState.HasMemorized;
							GUIParts.drawButton("MR", defaultColor, () => {
								curState.recallMemory();
							}, GUILayout.ExpandHeight(true));
							GUI.enabled = true;
						GUILayout.EndVertical();
					GUILayout.EndHorizontal();
				GUILayout.EndVertical();
                
				GUILayout.BeginVertical(GUIParts.styleFramed);
					// total delta-V display
					GUIParts.drawDoubleLabel("Total Δv:", GUIParts.leftColumnDataWidth, curState.currentMagnitude().ToString("0.##") + " m/s", 130);
					drawEAngle();
					drawEncounter(defaultColor);
				GUILayout.EndVertical();
					// Conics mode controls
					if (options.showConics) {
						GUIParts.drawConicsControls(options);
					}
				// trip info button and vessel focus buttons
				GUILayout.BeginHorizontal(GUIParts.styleFrameless);
					GUIParts.drawButton("Trip Info", (options.showTrip?Color.yellow:defaultColor), () => { options.showTrip = !options.showTrip; });
					GUIParts.drawButton("Focus on Vessel", defaultColor, () => {
						MapObject mapObject = PlanetariumCamera.fetch.targets.Find(o => (o.vessel != null) && o.vessel.Equals(FlightGlobals.ActiveVessel));
						MapView.MapCamera.SetTarget(mapObject);
					});
				GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		private void drawEAngle() {
			// Ejection angle
			if(options.showEAngle) {
				String eangle = "n/a";
				if (!FlightGlobals.ActiveVessel.orbit.referenceBody.isSun()) {
					double angle = FlightGlobals.ActiveVessel.orbit.getEjectionAngle(curState.node);
					if (!double.IsNaN(angle)) {
						eangle = Math.Abs(angle).ToString("0.##") + "° from " + ((angle >= 0) ? "prograde" : "retrograde");
					}
				}
                GUIParts.drawDoubleLabel("Ejection angle:", GUIParts.leftColumnDataWidth, eangle, 150);

				String einclination = "n/a";
				if (!FlightGlobals.ActiveVessel.orbit.referenceBody.isSun()) {
					double angle = FlightGlobals.ActiveVessel.orbit.getEjectionInclination(curState.node);
					if (!double.IsNaN(angle)) {
						einclination = Math.Abs(angle).ToString("0.##") + "° " + ((angle >= 0) ? "north" : "south");
					}
				}
                GUIParts.drawDoubleLabel("Eject. inclination:", GUIParts.leftColumnDataWidth, einclination, 150);
			}
		}

		private void drawEncounter(Color defaultColor) {
			// Additional Information
			if(options.showOrbitInfo) {
				// Find the next encounter, if any, in our flight plan.
				if(showEncounter) {
					Orbit nextEnc = curState.node.findNextEncounter();
					string name = "n/a";
					string theName = "n/a";
					string PeA = "n/a";

					if(nextEnc != null) {
						name = nextEnc.referenceBody.name;
						theName = nextEnc.referenceBody.theName;
						PeA = nextEnc.PeA.formatMeters();
					} else {
						curState.encounter = false;
					}
					// Next encounter periapsis
                    GUIParts.drawDoubleLabel("(" + name + ") Pe:", GUIParts.leftColumnDataWidth, PeA, 130);
					GUILayout.BeginHorizontal();
                    GUILayout.Label("", GUILayout.Width(GUIParts.leftColumnDataWidth));
					GUIParts.drawButton("Focus on " + theName, defaultColor, () => {
						MapObject mapObject = PlanetariumCamera.fetch.targets.Find(o => (o.celestialBody != null) && (o.celestialBody.name == name));
						MapView.MapCamera.SetTarget(mapObject);
					});
					GUILayout.EndHorizontal();
				} else {
					if(curState.node.solver.flightPlan.Count > 1) {
						// output the apoapsis and periapsis of our projected orbit.
                        GUIParts.drawDoubleLabel("Apoapsis:", GUIParts.leftColumnDataWidth, curState.node.nextPatch.ApA.formatMeters(), 100);
                        GUIParts.drawDoubleLabel("Periapsis:", GUIParts.leftColumnDataWidth, curState.node.nextPatch.PeA.formatMeters(), 130);
					}
				}
			}
		}

		private void drawTimeControls(Color contentColor) {
			// Universal time controls
			GUILayout.BeginHorizontal();
            GUILayout.Label((options.largeUTIncrement ? "UT: (x10 inc)" : "UT:"), GUILayout.Width(GUIParts.leftColumnNodeWidth));
			if(!curState.timeParsed) {
				GUI.contentColor = Color.red;
			}
            string check = GUILayout.TextField(curState.timeText, GUILayout.ExpandWidth(true));
			if(!curState.timeText.Equals(check, StringComparison.Ordinal)) {
				curState.setUT(check);
			}
			GUI.contentColor = contentColor;
			double currentUT = curState.currentUT();
			double ut_increment = options.increment * (options.largeUTIncrement ? 10.0 : 1.0);
			GUIParts.drawPlusMinusButtons(() => { curState.addUT(ut_increment); }, () => { curState.addUT(-ut_increment); },
				true, curState.node.patch.isUTInsidePatch(currentUT - ut_increment));
			GUILayout.EndHorizontal();

			// extended time controls
			if(options.showUTControls) {
				Orbit targ = NodeTools.getTargetOrbit();

				GUILayout.BeginHorizontal();
				GUIParts.drawButton("Peri", Color.yellow, () => { curState.setPeriapsis(); });
				GUI.enabled = curState.node.hasDN(targ);
				GUIParts.drawButton("DN", Color.magenta, () => {
					if(targ != null) {
						curState.setUT(curState.node.patch.getTargetDNUT(targ));
					} else {
						curState.setUT(curState.node.patch.getEquatorialDNUT());
					}
				});
				if (options.largeUTIncrement) {
					GUI.enabled = curState.node.patch.isUTInsidePatch(currentUT - curState.node.patch.period);
					GUIParts.drawButton("-Orb", Color.red, () => { curState.addUT(-curState.node.patch.period); });
					GUI.enabled = curState.node.patch.isUTInsidePatch(currentUT + curState.node.patch.period);
					GUIParts.drawButton("+Orb", Color.green, () => { curState.addUT(curState.node.patch.period); });
				} else {
					GUI.enabled = curState.node.patch.isUTInsidePatch(currentUT - 1000);
					GUIParts.drawButton("-1K", Color.red, () => { curState.addUT(-1000); });
					GUI.enabled = true;
					GUIParts.drawButton("+1K", Color.green, () => { curState.addUT(1000); });
				}
				GUI.enabled = curState.node.hasAN(targ);
				GUIParts.drawButton("AN", Color.cyan, () => {
					if(targ != null) {
						curState.setUT(curState.node.patch.getTargetANUT(targ));
					} else {
						curState.setUT(curState.node.patch.getEquatorialANUT());
					}
				});
				GUI.enabled = curState.node.patch.hasAP();
				GUIParts.drawButton("Apo", Color.blue, () => { curState.setApoapsis(); });
				GUILayout.EndHorizontal();
			}

			GUI.enabled = true;
		}

		private void drawProgradeControls(Color contentColor) {
			Color oldContentColor = GUI.contentColor;
			Color oldBackgroundColor = GUI.backgroundColor;
			// Prograde controls
			GUILayout.BeginHorizontal();
			GUI.contentColor = PROGRADE_COLOR;
            GUILayout.Label("Prograde:", GUILayout.Width(GUIParts.leftColumnNodeWidth));
			if (!curState.progradeParsed) {
				GUI.contentColor = Color.red;
				GUI.backgroundColor = Color.red;
			}
            string check = GUILayout.TextField(curState.progradeText, GUIParts.styleNodeControlTextField, GUILayout.Width(GUIParts.nodeControlTextFieldWidth));
			if(!curState.progradeText.Equals(check, StringComparison.Ordinal)) {
				curState.setPrograde(check);
			}
            //GUI.contentColor = contentColor;
			GUIParts.drawPlusMinusButtons(() => {
				curState.addPrograde(options.increment);
			}, () => {
				curState.addPrograde(-options.increment);
			});
			GUILayout.EndHorizontal();
            GUI.contentColor = oldContentColor;
            GUI.backgroundColor = oldBackgroundColor;
		}

		private void drawNormalControls(Color contentColor) {
			Color oldContentColor = GUI.contentColor;
			Color oldBackgroundColor = GUI.backgroundColor;
			// Normal controls
			GUILayout.BeginHorizontal();
			GUI.contentColor = NORMAL_COLOR;
            GUILayout.Label("Normal:", GUILayout.Width(GUIParts.leftColumnNodeWidth));
			if (!curState.normalParsed) {
				GUI.contentColor = Color.red;
				GUI.backgroundColor = Color.red;
			}
			string check = GUILayout.TextField(curState.normalText, GUIParts.styleNodeControlTextField, GUILayout.Width(GUIParts.nodeControlTextFieldWidth));
			if (!curState.normalText.Equals(check, StringComparison.Ordinal)) {
				curState.setNormal(check);
			}
			//GUI.contentColor = contentColor;
			GUIParts.drawPlusMinusButtons(() => {
				curState.addNormal(options.increment);
			}, () => {
				curState.addNormal(-options.increment);
			});
			GUILayout.EndHorizontal();
            GUI.contentColor = oldContentColor;
            GUI.backgroundColor = oldBackgroundColor;
		}

		private void drawRadialControls(Color contentColor) {
			Color oldContentColor = GUI.contentColor;
			Color oldBackgroundColor = GUI.backgroundColor;
			// radial controls
			GUILayout.BeginHorizontal();
			GUI.contentColor = RADIAL_COLOR;
            GUILayout.Label("Radial:", GUILayout.Width(GUIParts.leftColumnNodeWidth));
			if (!curState.radialParsed) {
				GUI.contentColor = Color.red;
				GUI.backgroundColor = Color.red;
			}
			string check = GUILayout.TextField(curState.radialText, GUIParts.styleNodeControlTextField, GUILayout.Width(GUIParts.nodeControlTextFieldWidth));
			if (!curState.radialText.Equals(check, StringComparison.Ordinal)) {
				curState.setRadial(check);
			}
			//GUI.contentColor = contentColor;
			GUIParts.drawPlusMinusButtons(() => {
				curState.addRadial(options.increment);
			}, () => {
				curState.addRadial(-options.increment);
			});
			GUILayout.EndHorizontal();
            GUI.contentColor = oldContentColor;
            GUI.backgroundColor = oldBackgroundColor;
		}

		private void drawManeuverPager() {
			PatchedConicSolver solver = NodeTools.getSolver();

			int idx = solver.maneuverNodes.IndexOf(curState.node);
			int count = solver.maneuverNodes.Count;

			GUILayout.BeginHorizontal();
			GUI.enabled = count > 1;
            if (GUILayout.Button("◀", GUIParts.stylePagerButtons))
            {
				if (idx > 0) {
					curState.nextNode = solver.maneuverNodes[idx - 1];
				} else {
					curState.nextNode = solver.maneuverNodes[count - 1];
				}
				curState.clearMemory();
			}
			GUI.enabled = true;
            if (GUILayout.Button("Node " + (idx + 1), GUIParts.stylePagerButtons))
            {
				MapView.MapCamera.SetTarget(curState.node.scaledSpaceTarget);
			}
			GUIParts.drawButton("Del", Color.red, () => {
				solver.RemoveManeuverNode(curState.node);
				curState.clearMemory();
			}, GUIParts.stylePagerButtons);
			GUI.enabled = count > 1;
            if (GUILayout.Button("▶", GUIParts.stylePagerButtons))
            {
				if (idx < (count - 1)) {
					curState.nextNode = solver.maneuverNodes[idx + 1];
				} else {
					curState.nextNode = solver.maneuverNodes[0];
				}
				curState.clearMemory();
			}
			GUI.enabled = true;
			GUILayout.EndHorizontal();
		}
		
		/// <summary>
		/// Draws the Clock window.
		/// </summary>
		private void drawClockWindow() {
			Color defaultColor = GUI.backgroundColor;
			double timeNow = Planetarium.GetUniversalTime();
			String timeUT = timeNow.ToString("F0");
			String timeHuman = timeNow.convertUTtoHumanTime();

			GUILayout.BeginVertical();

			GUIParts.drawDoubleLabel("Time:", 35, timeHuman, 150);
			GUIParts.drawDoubleLabel("UT:", 35, Math.Floor(timeNow).ToString("F0"), 150);

			if(showTimeNext) {
				double next = 0.0;
				string labelText = "";
				if(NodeTools.getSolver().maneuverNodes.Count > 0) {
					// protection from index out of range errors.
					// should probably handle this better.
					next = timeNow - NodeTools.getSolver().maneuverNodes[0].UT;
				}
				if(next < 0) {
					labelText = "T-" + next.convertUTtoHumanDuration();
				} else {
					labelText = "T+" + next.convertUTtoHumanDuration();
				}
				GUIParts.drawDoubleLabel("Next:", 35, labelText, 150);
			}

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Draws the Conics window.
		/// </summary>
		private void drawConicsWindow() {
			GUILayout.BeginVertical();
			GUIParts.drawConicsControls(options);
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Draws the Options window.
		/// </summary>
		private void drawOptionsWindow() {
			Color defaultColor = GUI.backgroundColor;

			// Close button
			if(GUI.Button(new Rect(options.optionsWindowPos.width - 24, 2, 22, 18), "X")) {
				showOptions = false;
			}

			GUILayout.BeginVertical();

			// use a temp variable so we can check whether the main window needs resizing.
			bool temp;

			temp = GUILayout.Toggle(options.showConics, "Show conics controls");
			if (temp != options.showConics) {
				options.showConics = temp;
				curState.resizeMainWindow = true;
			}

			options.showConicsAlways = GUILayout.Toggle(options.showConicsAlways, "Show conics window");
			options.showClock = GUILayout.Toggle(options.showClock, "Show clock window");
			temp = GUILayout.Toggle(options.showManeuverPager, "Show maneuver pager");
			if(temp != options.showManeuverPager) {
				options.showManeuverPager = temp;
				curState.resizeMainWindow = true;
			}
			temp = GUILayout.Toggle(options.showUTControls, "Show additional UT controls");
			if(temp != options.showUTControls) {
				options.showUTControls = temp;
				curState.resizeMainWindow = true;
			}
			options.largeUTIncrement = GUILayout.Toggle(options.largeUTIncrement, "Use x10 UT increment");
			temp = GUILayout.Toggle(options.showEAngle, "Show ejection angle");
			if(temp != options.showEAngle) {
				options.showEAngle = temp;
				curState.resizeMainWindow = true;
			}
			temp = GUILayout.Toggle(options.showOrbitInfo, "Show orbit information");
			if(temp != options.showOrbitInfo) {
				options.showOrbitInfo = temp;
				curState.resizeMainWindow = true;
			}

            temp = GUILayout.Toggle(options.useKspSkin, "Use SKP Skin");
            if (temp != options.useKspSkin) {
                options.useKspSkin = temp;
                curState.resizeMainWindow = true;
                curState.resizeClockWindow = true;
				curState.resizeOtherWindows = true;
            }

#if NODE_CLEANUP
			options.removeUsedNodes = GUILayout.Toggle(options.removeUsedNodes, "Remove used nodes");
            //TODO: Add threshold controls for removing used nodes
#endif

			if (UpdateChecker.Done && (UpdateChecker.UpdateAvailable == true)) {
				GUILayout.Space(5);
				Color oldColor = GUI.color;
				GUI.color = Color.yellow;
				GUILayout.Label("An update to this plugin is available.");
				GUI.color = oldColor;
			}

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Draws the Keymapper window.
		/// </summary>
		private void drawKeymapperWindow() {
			Color defaultColor = GUI.backgroundColor;

			// Close button
			if(GUI.Button(new Rect(options.keymapperWindowPos.width - 24, 2, 22, 18), "X")) {
				showKeymapper = false;
			}

			GUILayout.BeginVertical();

			// Set window control
			drawKeyControls("Hide/show window", Key.HIDEWINDOW, options.hideWindow);

			// Set add node widget button
			drawKeyControls("Open node gizmo", Key.ADDWIDGET, options.addWidget);

			// Set prograde controls
			drawKeyControls("Increment prograde", Key.PROGINC, options.progInc);
			drawKeyControls("Decrement prograde", Key.PROGDEC, options.progDec);

			// set normal controls
			drawKeyControls("Increment normal", Key.NORMINC, options.normInc);
			drawKeyControls("Decrement normal", Key.NORMDEC, options.normDec);

			// set radial controls
			drawKeyControls("Increment radial", Key.RADIINC, options.radiInc);
			drawKeyControls("Decrement radial", Key.RADIDEC, options.radiDec);

			// set time controls
			drawKeyControls("Increment time", Key.TIMEINC, options.timeInc);
			drawKeyControls("Decrement time", Key.TIMEDEC, options.timeDec);

			// set paging controls
			drawKeyControls("Page increment", Key.PAGEINC, options.pageIncrement);
			drawKeyControls("Page conics", Key.PAGECON, options.pageConics);

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		private void drawKeyControls(string title, Key key, KeyCode currentKeyCode) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(title + ": " + currentKeyCode.ToString(), GUILayout.Width(200));
			GUIParts.drawButton("Set", GUI.backgroundColor, () => {
				doWaitForKey("Press a key to bind " + title.ToLower() + "...", key);
			});
			GUILayout.EndHorizontal();
		}

		private void drawTripWindow() {
			PatchedConicSolver solver = NodeTools.getSolver();

			GUILayout.BeginVertical();
			if(solver.maneuverNodes.Count < 1) {
				GUILayout.BeginHorizontal();
				GUILayout.Label("No nodes to show.", GUILayout.Width(200));
				GUILayout.EndHorizontal();
			} else {
				double total = 0.0;
				double timeNow = Planetarium.GetUniversalTime();

				GUILayout.BeginHorizontal();
				GUILayout.Label("", GUILayout.Width(60));
				GUILayout.Label("Δv", GUILayout.Width(90));
				GUILayout.Label("Time Until", GUILayout.Width(200));
				GUILayout.Label("", GUILayout.Width(120));
				GUILayout.EndHorizontal();

				foreach(ManeuverNode curNode in solver.maneuverNodes) {
					int idx = solver.maneuverNodes.IndexOf(curNode);
					double timeDiff = curNode.UT - timeNow;
					GUILayout.BeginHorizontal();
					GUILayout.Label("Node " + (idx + 1), GUILayout.Width(60));
					GUILayout.Label(curNode.DeltaV.magnitude.ToString("F2") + " m/s", GUILayout.Width(90));
					GUILayout.Label(timeDiff.convertUTtoHumanDuration(), GUILayout.Width(200));
					if(idx > 0) {
						GUIParts.drawButton("▲ Merge", Color.white, () => {
							// schedule for next layout pass to not mess up maneuver nodes while iterating over them
							scheduledForLayout.Add(() => {
								solver.maneuverNodes[idx].mergeNodeDown();
							});
						});
					}
					GUILayout.EndHorizontal();
					total += curNode.DeltaV.magnitude;
				}

				GUILayout.BeginHorizontal();
				GUILayout.Label("Total", GUILayout.Width(60));
				GUILayout.Label(total.ToString("F2") + " m/s", GUILayout.Width(90));
				GUILayout.Label("", GUILayout.Width(200));
				GUILayout.EndHorizontal();
			}

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Returns whether the Node Editor can be shown based on a number of global factors.
		/// </summary>
		/// <value><c>true</c> if the Node Editor can be shown; otherwise, <c>false</c>.</value>
		private bool canShowNodeEditor {
			get {
				return (FlightGlobals.ActiveVessel != null) && MapView.MapIsEnabled && (NodeTools.getSolver().maneuverNodes.Count > 0);
			}
		}

		/// <summary>
		/// Returns whether the Conics Window can be shown based on a number of global factors.
		/// </summary>
		/// <value><c>true</c> if the Conics Window can be shown; otherwise, <c>false</c>.</value>
		private bool canShowConicsWindow {
			get {
				return (FlightGlobals.ActiveVessel != null) && MapView.MapIsEnabled && options.showConicsAlways;
			}
		}

		/// <summary>
		/// Returns whether the Clock Window can be shown based on a number of global factors.
		/// </summary>
		/// <value><c>true</c> if the Clock Window can be shown; otherwise, <c>false</c>.</value>
		private bool canShowClock {
			get {
				return (FlightGlobals.ActiveVessel != null) && options.showClock;
			}
		}


		private void doWaitForKey(String msg, Key key) {
			ScreenMessages.PostScreenMessage(msg, 5.0f, ScreenMessageStyle.UPPER_CENTER);
			waitForKey = true;
			currentWaitKey = key;
			keyWaitTime = Planetarium.GetUniversalTime();
		}

		/// <summary>
		/// Processes keyboard input.
		/// </summary>
		private void processKeyInput() {
			if(!Input.anyKeyDown) {
				return;
			}

			// Fix for a bug in Linux where typing would still control game elements even if
			// a textbox was focused.
			if(GUIUtility.keyboardControl != 0) {
				return;
			}

			// process any key input for settings
			if(waitForKey) {
				KeyCode key = NodeTools.fetchKey();
				// if the time is up or we have no key to process, reset.
				if(((keyWaitTime + 5.0) < Planetarium.GetUniversalTime()) || key == KeyCode.None) {
					currentWaitKey = Key.NONE;
					waitForKey = false;
					return;
				}

				// which key are we waiting for?
				switch(currentWaitKey) {
					case Key.PROGINC:
						options.progInc = key;
						break;
					case Key.PROGDEC:
						options.progDec = key;
						break;
					case Key.NORMINC:
						options.normInc = key;
						break;
					case Key.NORMDEC:
						options.normDec = key;
						break;
					case Key.RADIINC:
						options.radiInc = key;
						break;
					case Key.RADIDEC:
						options.radiDec = key;
						break;
					case Key.TIMEINC:
						options.timeInc = key;
						break;
					case Key.TIMEDEC:
						options.timeDec = key;
						break;
					case Key.PAGEINC:
						options.pageIncrement = key;
						break;
					case Key.PAGECON:
						options.pageConics = key;
						break;
					case Key.HIDEWINDOW:
						options.hideWindow = key;
						break;
					case Key.ADDWIDGET:
						options.addWidget = key;
						break;
				}
				currentWaitKey = Key.NONE;
				waitForKey = false;
				return;
			}

			// process normal keyboard input
			// change increment
			if(Input.GetKeyDown(options.pageIncrement)) {
				if(Event.current.alt) {
					options.downIncrement();
				} else {
					options.upIncrement();
				}
			}
			// prograde increment
			if(Input.GetKeyDown(options.progInc)) {
				curState.addPrograde(options.increment);
			}
			// prograde decrement
			if(Input.GetKeyDown(options.progDec)) {
				curState.addPrograde(-options.increment);
			}
			// normal increment
			if(Input.GetKeyDown(options.normInc)) {
				curState.addNormal(options.increment);
			}
			// normal decrement
			if(Input.GetKeyDown(options.normDec)) {
				curState.addNormal(-options.increment);
			}
			// radial increment
			if(Input.GetKeyDown(options.radiInc)) {
				curState.addRadial(options.increment);
			}
			// radial decrement
			if(Input.GetKeyDown(options.radiDec)) {
				curState.addRadial(-options.increment);
			}
			// UT increment
			if(Input.GetKeyDown(options.timeInc)) {
				curState.addUT(options.increment * (options.largeUTIncrement ? 10.0 : 1.0));
			}
			// UT decrement
			if(Input.GetKeyDown(options.timeDec)) {
				curState.addUT(-options.increment * (options.largeUTIncrement ? 10.0 : 1.0));
			}
			// Page Conics
			if(Input.GetKeyDown(options.pageConics)) {
				options.pageConicsMode();
			}
			// hide/show window
			if(Input.GetKeyDown(options.hideWindow)) {
				shown = !shown;
			}
			// open node gizmo
			if(Input.GetKeyDown(options.addWidget)) {
				curState.node.CreateNodeGizmo();
			}
		}

		/// <summary>
		/// Load any saved configuration from file.
		/// </summary>
		private void loadConfig() {
			Debug.Log("Loading PreciseNode settings.");
			if(!configLoaded) {
				config = KSP.IO.PluginConfiguration.CreateForType<PreciseNode>(null);
				config.load();
				configLoaded = true;

				try {
					options.conicsMode = config.GetValue<int>("conicsMode", 3);
					options.mainWindowPos.x = config.GetValue<int>("mainWindowX", Screen.width / 10);
					options.mainWindowPos.y = config.GetValue<int>("mainWindowY", 20);
					options.optionsWindowPos.x = config.GetValue<int>("optWindowX", Screen.width / 3);
					options.optionsWindowPos.y = config.GetValue<int>("optWindowY", 20);
					options.keymapperWindowPos.x = config.GetValue<int>("keyWindowX", Screen.width / 5);
					options.keymapperWindowPos.y = config.GetValue<int>("keyWindowY", 20);
					options.clockWindowPos.x = config.GetValue<int>("clockWindowX", Screen.width / 3);
					options.clockWindowPos.y = config.GetValue<int>("clockWindowY", Screen.height / 2);
					options.conicsWindowPos.x = config.GetValue<int>("conicsWindowX", Screen.width / 5);
					options.conicsWindowPos.y = config.GetValue<int>("conicsWindowY", Screen.height / 2);
					options.tripWindowPos.x = config.GetValue<int>("tripWindowX", Screen.width / 5);
					options.tripWindowPos.y = config.GetValue<int>("tripWindowY", Screen.height / 5);
					options.showClock = config.GetValue<bool>("showClock", false);
					options.showEAngle = config.GetValue<bool>("showEAngle", true);
					options.showConics = config.GetValue<bool>("showConics", true);
					options.showConicsAlways = config.GetValue<bool>("showConicsAlways", false);
					options.showOrbitInfo = config.GetValue<bool>("showOrbitInfo", false);
					options.showUTControls = config.GetValue<bool>("showUTControls", false);
					options.showManeuverPager = config.GetValue<bool>("showManeuverPager", true);
#if NODE_CLEANUP
					options.removeUsedNodes = config.GetValue<bool>("removeUsedNodes", false);
					options.usedNodeThreshold = config.GetValue<double>("usedNodeThreshold", 0.5);
#endif
					options.largeUTIncrement = config.GetValue<bool>("largeUTIncrement", false);
                    options.useKspSkin = config.GetValue<bool>("useKspSkin", false);

					string temp = config.GetValue<String>("progInc", "Keypad8");
					options.progInc = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("progDec", "Keypad5");
					options.progDec = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("normInc", "Keypad9");
					options.normInc = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("normDec", "Keypad7");
					options.normDec = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("radiInc", "Keypad6");
					options.radiInc = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("radiDec", "Keypad4");
					options.radiDec = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("timeInc", "Keypad3");
					options.timeInc = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("timeDec", "Keypad1");
					options.timeDec = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("pageIncrement", "Keypad0");
					options.pageIncrement = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("pageConics", "KeypadEnter");
					options.pageConics = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("hideWindow", "P");
					options.hideWindow = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
					temp = config.GetValue<String>("addWidget", "O");
					options.addWidget = (KeyCode)Enum.Parse(typeof(KeyCode), temp);
				} catch(ArgumentException) {
					// do nothing here, the defaults are already set
				}
			}
		}

		/// <summary>
		/// Save our configuration to file.
		/// </summary>
		private void saveConfig() {
			Debug.Log("Saving PreciseNode settings.");
			if(!configLoaded) {
				config = KSP.IO.PluginConfiguration.CreateForType<PreciseNode>(null);
			}

			config["conicsMode"] = options.conicsMode;
			config["progInc"] = options.progInc.ToString();
			config["progDec"] = options.progDec.ToString();
			config["normInc"] = options.normInc.ToString();
			config["normDec"] = options.normDec.ToString();
			config["radiInc"] = options.radiInc.ToString();
			config["radiDec"] = options.radiDec.ToString();
			config["timeInc"] = options.timeInc.ToString();
			config["timeDec"] = options.timeDec.ToString();
			config["pageIncrement"] = options.pageIncrement.ToString();
			config["pageConics"] = options.pageConics.ToString();
			config["hideWindow"] = options.hideWindow.ToString();
			config["addWidget"] = options.addWidget.ToString();
			config["mainWindowX"] = (int)options.mainWindowPos.x;
			config["mainWindowY"] = (int)options.mainWindowPos.y;
			config["optWindowX"] = (int)options.optionsWindowPos.x;
			config["optWindowY"] = (int)options.optionsWindowPos.y;
			config["keyWindowX"] = (int)options.keymapperWindowPos.x;
			config["keyWindowY"] = (int)options.keymapperWindowPos.y;
			config["clockWindowX"] = (int)options.clockWindowPos.x;
			config["clockWindowY"] = (int)options.clockWindowPos.y;
			config["conicsWindowX"] = (int)options.conicsWindowPos.x;
			config["conicsWindowY"] = (int)options.conicsWindowPos.y;
			config["tripWindowX"] = (int)options.tripWindowPos.x;
			config["tripWindowY"] = (int)options.tripWindowPos.y;
			config["showClock"] = options.showClock;
			config["showEAngle"] = options.showEAngle;
			config["showConics"] = options.showConics;
			config["showConicsAlways"] = options.showConicsAlways;
			config["showOrbitInfo"] = options.showOrbitInfo;
			config["showUTControls"] = options.showUTControls;
			config["showManeuverPager"] = options.showManeuverPager;
#if NODE_CLEANUP
			config["removeUsedNodes"] = options.removeUsedNodes;
			config["usedNodeThreshold"] = options.usedNodeThreshold;
#endif
			config["largeUTIncrement"] = options.largeUTIncrement;
            config["useKspSkin"] = options.useKspSkin;

			config.save();
		}
	}	
}

