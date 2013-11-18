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
		public new DisplayMesh Mesh { get; set; }

		/// <value> The count of triangles in the original mesh. </value>
		public new uint TriangleCount { get { return Mesh.TriangleCount; } }

		/// <value> True if the mesh is opaque. </value>
		public new bool IsOpaque { get { return Mesh.IsOpaque; } }

		/// <value> The transform of this instance of the mesh, not the original transform. </value>
		public new Transform XForm { get { return m_xform; } }
		#endregion

		#region constructors
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