//
// DisplayInstanceMesh.cs
// RhinoMobile.Display
//
// Created by dan (dan@mcneel.com) on 9/17/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using Rhino.Geometry;

namespace RhinoMobile.Display
{
	public class DisplayInstanceMesh : DisplayObject
	{
		#region members
		private readonly Transform m_xform;
		#endregion

		#region properties
		/// <value> The original DisplayMesh. </value>
		public override DisplayMesh Mesh { get; protected set; }

		/// <value> The count of triangles in the original mesh. </value>
		public override uint TriangleCount { get { return Mesh.TriangleCount; } }

		/// <value> True if the mesh is opaque. </value>
		public override bool IsOpaque { get { return Mesh.IsOpaque; } }

		/// <value> The transform of this instance of the mesh, not the original transform. </value>
		public override Transform XForm { get { return m_xform; } }
		#endregion

		#region constructors and disposal
		/// <summary>
		/// Creates an instance of a DisplayMesh with a new transform.
		/// </summary>
		public DisplayInstanceMesh (DisplayMesh mesh, Transform xform)
		{
			Mesh = mesh;
			IsVisible = mesh.IsVisible;
			LayerIndex = mesh.LayerIndex;
			m_xform = xform;

			Transform zeroXForm = CreateZeroTransform ();

			if (m_xform.Equals(zeroXForm))
				m_xform = Transform.Identity;
		}


		/// <summary>
		/// Passively reclaims unmanaged resources when the class user did not explicitly call Dispose().
		/// </summary>
		~ DisplayInstanceMesh () { Dispose (false); }

		/// <summary>
		/// Actively reclaims unmanaged resources that this instance uses.
		/// </summary>
		public new void Dispose()
		{
			try {
				Dispose(true);
				GC.SuppressFinalize(this);
			}
			finally {
				base.Dispose ();
			}
		}

		/// <summary>
		/// <para>This method is called with argument true when class user calls Dispose(), while with argument false when
		/// the Garbage Collector invokes the finalizer, or Finalize() method.</para>
		/// <para>You must reclaim all used unmanaged resources in both cases, and can use this chance to call Dispose on disposable fields if the argument is true.</para>
		/// </summary>
		/// <param name="disposing">true if the call comes from the Dispose() method; false if it comes from the Garbage Collector finalizer.</param>
		private new void Dispose (bool disposing)
		{
			// Free unmanaged resources...

			// Free managed resources...but only if called from Dispose
			// (If called from Finalize then the objects might not exist anymore)
			if (disposing) {
				if (Mesh != null) {
					Mesh.Dispose ();
					Mesh = null;
				}
			}	
		}
		#endregion

		#region methods
		/// <returns> A zero'd out 4 x 4 transformation matrix with M33 remaining * </returns>
		private Transform CreateZeroTransform () {
			Transform zeroXform = new Transform ();
			zeroXform.M00 = 0; zeroXform.M01 = 0; zeroXform.M02 = 0; zeroXform.M03 = 0;
			zeroXform.M10 = 0; zeroXform.M11 = 0; zeroXform.M12 = 0; zeroXform.M13 = 0;
			zeroXform.M20 = 0; zeroXform.M21 = 0; zeroXform.M22 = 0; zeroXform.M23 = 0;
			zeroXform.M30 = 0; zeroXform.M31 = 0; zeroXform.M32 = 0; //M33 remains *
			return zeroXform;
		}
		#endregion
	
	}
}