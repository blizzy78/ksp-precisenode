using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/******************************************************************************
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
	[KSPAddon(KSPAddon.Startup.EveryScene, true)]
	internal class UpdateChecker : MonoBehaviour {
		private const string VERSION_URL = "http://blizzy.de/precise-node/version2.txt";

		internal static bool Done;
		internal static string[] KspVersions = null;
		internal static bool? UpdateAvailable;

		private WWW www;

		internal UpdateChecker() {
		}

		private void Update() {
			Log.trace("UpdateChecker.update()");

			if (!Done) {
				if (www == null) {
					Log.debug("getting version from {0}", VERSION_URL);
					www = new WWW(VERSION_URL);
				}

				if (www.isDone) {
					try {
						bool updateAvailable = false;
						int version = -1;
						if (String.IsNullOrEmpty(www.error)) {
							string text = www.text.Replace("\r", string.Empty);
							Log.debug("version text: {0}", text);
							string[] lines = text.Split(new char[] { '\n' }, StringSplitOptions.None);
							try {
								version = int.Parse(lines[0]);
								updateAvailable = version > PreciseNode.VERSION;
							} catch (Exception) {
								// ignore
							}
							KspVersions = lines[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
						}

						if (updateAvailable) {
							Log.info("update found: {0} vs {1}", version, PreciseNode.VERSION);
						} else {
							Log.info("no update found: {0} vs {1}", version, PreciseNode.VERSION);
						}
						UpdateAvailable = updateAvailable;
					} finally {
						www = null;
						Done = true;
					}
				}
			}
		}
	}
}
