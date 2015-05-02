using System;
using UnityEngine;
using KSP.IO;

/******************************************************************************
 * Copyright (c) 2013-2014, Justin Bengtson
 * Copyright (c) 2014-2015, Maik Schreiber
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
	internal static class GUIParts {
		internal static void drawDoubleLabel(String text1, float width1, String text2, float width2) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(text1, GUILayout.Width(width1));
			GUILayout.Label(text2, GUILayout.Width(width2));
			GUILayout.EndHorizontal();
		}

		internal static void drawButton(String text, Color bgColor, Action callback, params GUILayoutOption[] options) {
			Color defaultColor = GUI.backgroundColor;
			GUI.backgroundColor = bgColor;
			if(GUILayout.Button(text, options)) {
				callback();
			}
			GUI.backgroundColor = defaultColor;
		}

		internal static void drawConicsControls(PreciseNodeOptions options) {
			PatchedConicSolver solver = NodeTools.getSolver();
			Color defaultColor = GUI.backgroundColor;
			
			// Conics mode controls
			GUILayout.BeginHorizontal();
			GUILayout.Label("Conics mode: ", GUILayout.Width(100));
			for (int mode = 0; mode <= 4; mode++) {
				drawButton(mode.ToString(), (options.conicsMode == mode) ? Color.yellow : defaultColor, () => {
					options.setConicsMode(mode);
				});
			}
			GUILayout.EndHorizontal();

			// conics patch limit editor.
			GUILayout.BeginHorizontal();
			GUILayout.Label("Change conics samples:", GUILayout.Width(200));
			drawPlusMinusButtons(solver.IncreasePatchLimit, solver.DecreasePatchLimit);
			GUILayout.EndHorizontal();
		}

		internal static void drawPlusMinusButtons(Action plus, Action minus, bool plusEnabled = true, bool minusEnabled = true) {
			bool oldEnabled = GUI.enabled;
			GUI.enabled = plusEnabled || minusEnabled;
			drawButton("+/-", GUI.backgroundColor, () => {
				switch (Event.current.button) {
					case 0:
						if (plusEnabled) {
							plus();
						}
						break;
					case 1:
						if (minusEnabled) {
							minus();
						}
						break;
				}
			});
			GUI.enabled = oldEnabled;
		}
	}
}
