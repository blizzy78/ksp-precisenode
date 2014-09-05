using System;
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
	internal static class GUIParts {
        // styling code added by DaMichel
        private static class CurrentSkin
        {
            internal const int UNINITIALIZED = 0;
            internal const int KSP = 1;
            internal const int UNITY = 2;
        };
        private  static int currentSkin = CurrentSkin.UNINITIALIZED;
        private  static GUISkin  skin = null; // reference to the actual current skin
        internal static GUIStyle styleFramed = null; // for group
        internal static GUIStyle styleFrameless = null; // to get margins & padding around elements which are not grouped with a visible border
        internal static GUIStyle styleNodeControlTextField = null; // prograde, normal, radial text field style
        internal static GUIStyle styleDataDisplayText = null; // DV, ejection angle etc.
        internal static GUIStyle stylePagerButtons = null; // a little bit bigger
		// widths for stuff on the left column
        internal static float    leftColumnDataWidth = 110;
        internal static float    leftColumnNodeWidth = 80;
        internal static float    leftColumnWidth     = 100;
        internal static float    nodeControlTextFieldWidth = 90;
		// The SKP skin does not resize according to the width of toggle controls.
		// So we have to make a little hack. The content here should reflect the longest string among the options.
		// //float width = GUI.skin.toggle.CalcSize(new GUIContent("Show additional UT controls")).x;
		// Ups .. this also does not work so ...
		internal static float    optionsWindowSize;

		/* Set GUI.skin to the proper value. Construct a new GUISkin instance based on
		 * the either the KSP skin or the Unity skin. */
        internal static void initGUI(bool useKspSkin)
        {
            if (currentSkin != (useKspSkin ? CurrentSkin.KSP : CurrentSkin.UNITY))
            {
                if (useKspSkin)
                {
                    Color32 labelColor = new Color32(240, 240, 240, 255);

                    skin = (GUISkin)GUISkin.Instantiate(HighLogic.Skin); // make a copy of the KSP skin

					// The KSP skin has large top padding and a few pixels on the other edges.
					// I want a much thinner than default border.
                    RectOffset r = skin.window.padding;
                    skin.window.padding = new RectOffset(3,2,r.top,3);

                    styleFramed = skin.box;

                    styleFrameless = new GUIStyle();
                    styleFrameless.padding = styleFramed.padding;
                    styleFrameless.margin  = styleFramed.margin;
           
                    skin.button.alignment = TextAnchor.MiddleCenter;
                    skin.button.padding = new RectOffset(6, 6, 2, 1); // any lower than 6 pixel and single letter buttons aren't drawn correctly
                    skin.button.fontStyle = FontStyle.Normal;
            
                    skin.textField.padding = new RectOffset(5, 4, 2, 1);
                    skin.textField.alignment = TextAnchor.MiddleLeft;
                    skin.textField.fontStyle = FontStyle.Normal;

					// The text field text is yellowish by default which 
					// does not work when modulated with prograde/normal/radial colors.
					// Therefore the color is set to white.               
                    styleNodeControlTextField = new GUIStyle(skin.textField);
                    styleNodeControlTextField.normal.textColor = Color.white;
                    styleNodeControlTextField.hover.textColor = Color.white;
                    styleNodeControlTextField.active.textColor = Color.white;
                    styleNodeControlTextField.focused.textColor = Color.white;

                    skin.label.padding = new RectOffset(1, 1, 2, 1);
                    skin.label.alignment = TextAnchor.MiddleLeft;
                    skin.label.wordWrap = false;

                    styleDataDisplayText = new GUIStyle(skin.label);

                    skin.label.normal.textColor = labelColor; // make the labels light grey, and the data text default green

                    stylePagerButtons = new GUIStyle(skin.button);
                    stylePagerButtons.fontStyle = FontStyle.Bold;
                    stylePagerButtons.padding = new RectOffset(6, 6, 6, 4);

					optionsWindowSize = 230;

                    currentSkin = CurrentSkin.KSP;
                }
                else
                {
					// just copy the unity skin. 
                    GUI.skin = null; // this way we reset the current skin to the default
                    skin = GUI.skin; // and this actually returns a non-null reference.
                    styleFramed = new GUIStyle();
                    styleFramed.padding = skin.box.padding;
                    styleFramed.margin  = skin.box.margin;
                    styleFrameless = new GUIStyle();
                    styleFrameless.padding = skin.box.padding;
                    styleFrameless.margin = skin.box.margin;
                    styleNodeControlTextField = new GUIStyle(skin.textField);
                    styleDataDisplayText = new GUIStyle(skin.label); // DV, ejection angle etc.
                    stylePagerButtons = new GUIStyle(skin.button);
					optionsWindowSize = 0; // automatic sizing works here
                    currentSkin = CurrentSkin.UNITY;
                }
            }
            GUI.skin = skin;
        }

		internal static void drawDoubleLabel(String text1, float width1, String text2, float width2) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(text1, GUILayout.Width(width1));
            GUILayout.Label(text2, styleDataDisplayText, GUILayout.Width(width2));
			GUILayout.EndHorizontal();
		}

		internal static void drawButton(String text, Color bgColor, Action callback, GUIStyle style, params GUILayoutOption[] options) {
			Color defaultColor = GUI.backgroundColor;
			GUI.backgroundColor = bgColor;
			if(GUILayout.Button(text, style, options)) {
				callback();
			}
			GUI.backgroundColor = defaultColor;
		}

        internal static void drawButton(String text, Color bgColor, Action callback, params GUILayoutOption[] options)
        {
            drawButton(text, bgColor, callback, skin.button, options);
        }

		internal static void drawConicsControls(PreciseNodeOptions options) {
			PatchedConicSolver solver = NodeTools.getSolver();
			Color defaultColor = GUI.backgroundColor;
			// Conics mode controls
            GUILayout.BeginVertical(GUIParts.styleFramed);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Conics mode: ", GUILayout.Width(leftColumnWidth));
			    for (int mode = 0; mode <= 4; mode++) {
				    drawButton(mode.ToString(), (options.conicsMode == mode) ? Color.yellow : defaultColor, () => {
					    options.setConicsMode(mode);
				    });
			    }
                GUILayout.EndHorizontal();

			    // conics patch limit editor.
			    GUILayout.BeginHorizontal();
			    GUILayout.Label("Change conics samples:", GUILayout.ExpandWidth(true));
			    drawPlusMinusButtons(solver.IncreasePatchLimit, solver.DecreasePatchLimit);
			    GUILayout.EndHorizontal();
            GUILayout.EndVertical();
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
			}, skin.button);
			GUI.enabled = oldEnabled;
		}
	}
}
