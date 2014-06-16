//
// ModelMesh.cs
// RhinoMobile.Model
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
using System.Collections.Generic;

using Rhino.Geometry;
using RhinoMobile.Display;

namespace RhinoMobile.Model
{
	/// <summary>
	/// This class wraps meshes in the 3dm model itself
	/// </summary>
	public class ModelMesh : ModelObject
	{
		#region properties
		/// <value> Array of DisplayMeshes associated with this ModelMesh </value>
		public Object[] DisplayMeshes { get; protected set; }
		#endregion

		#region constructors and disposal
		/// <summary>
		/// Creates a new instance of ModelMesh from an array of meshes
		/// </summary>
		public ModelMesh (Object[] meshArray, Guid guid)
		{
			DisplayMeshes = meshArray;
			ObjectId = guid;
		}
			
		/// <summary>
		/// Passively reclaims unmanaged resources when the class user did not explicitly call Dispose().
		/// </summary>
		~ ModelMesh () { Dispose (false); }

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
				DisplayMeshes = null;
			}	
		}
		#endregion

		#region methods
		/// <summary>
		/// Explodes a list of DisplayObjects into DisplayMeshes
		/// </summary>
		public new void ExplodeIntoArray(RMModel model, List<DisplayObject> array, Transform xform)
		{
			bool isIdentityXform = xform.Equals (Transform.Identity);

			if (DisplayMeshes == null)
				return;

			foreach (DisplayMesh mesh in DisplayMeshes) {
				if (isIdentityXform) {
					mesh.IsVisible = Visible;
					mesh.LayerIndex = LayerIndex;
					array.Add (mesh);
				} else {
					DisplayInstanceMesh instanceMesh = new DisplayInstanceMesh (mesh, xform);
					instanceMesh.IsVisible = Visible;
					instanceMesh.LayerIndex = LayerIndex;
					array.Add (instanceMesh);
				}
			}
		}
		#endregion

	}
}