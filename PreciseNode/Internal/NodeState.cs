using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
	internal class NodeState : ICloneable {
		internal Vector3d deltaV;
		internal double UT;

		internal NodeState() {
			deltaV = new Vector3d();
			UT = 0;
		}

		internal NodeState(Vector3d dv, double u) {
			deltaV = new Vector3d(dv.x, dv.y, dv.z);
			UT = u;
		}

		internal NodeState(ManeuverNode m) {
			deltaV = new Vector3d(m.DeltaV.x, m.DeltaV.y, m.DeltaV.z);
			UT = m.UT;
		}

		internal void update(ManeuverNode m) {
			deltaV.x = m.DeltaV.x;
			deltaV.y = m.DeltaV.y;
			deltaV.z = m.DeltaV.z;
			UT = m.UT;
		}

		internal Vector3d getVector() {
			return new Vector3d(deltaV.x, deltaV.y, deltaV.z);
		}

		internal bool compare(ManeuverNode m) {
			if (deltaV.x != m.DeltaV.x || deltaV.y != m.DeltaV.y || deltaV.z != m.DeltaV.z || UT != m.UT) {
				return false;
			}
			return true;
		}

		internal void createManeuverNode(PatchedConicSolver p) {
			ManeuverNode newnode = p.AddManeuverNode(UT);
			newnode.OnGizmoUpdated(deltaV, UT);
		}

		public object Clone() {
			return MemberwiseClone();
		}
	}
}
