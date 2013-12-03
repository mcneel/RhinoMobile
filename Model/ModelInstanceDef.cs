//
// ModelInstanceDef.cs
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
using RhinoMobile;
using RhinoMobile.Display;

namespace RhinoMobile.Model
{
	public class ModelInstanceDef : ModelObject
	{
		#region properties
		/// <value> This object's instance definition geometry. </value>
		public InstanceDefinitionGeometry InstanceDefinition { get; set; }
		#endregion

		#region constructors
		/// <summary>
		/// Creates a model instance definition from an instance definition geometry
		/// </summary>
		public ModelInstanceDef (InstanceDefinitionGeometry idef)
		{
			InstanceDefinition = idef;
			ObjectId = idef.Id;
		}
		#endregion

		#region methods
		/// <summary>
		/// Explodes each object in the instance definition into a list of display objects
		/// </summary>
		public new void ExplodeIntoArray(RMModel model, List<DisplayObject> array, Transform xform)
		{
			RMModel currentModel = model;

			int instanceCount = InstanceDefinition.GetObjectIds ().GetLength (0);

			for (int i = 0; i < instanceCount; i++) {
				Guid guid = InstanceDefinition.GetObjectIds () [i];
				ModelObject modelObject = currentModel.ModelObjectWithGUID (guid);
				if (modelObject != null) {
					try {
						(modelObject as ModelMesh).LayerIndex = LayerIndex;
						(modelObject as ModelMesh).ExplodeIntoArray (model, array, xform);
					} catch (SystemException ex) {
						Console.WriteLine ("Caught Exception: " + ex.Message);
						Console.WriteLine ("This is caused by a null Mesh on an InstanceRef");
					}
				}

			}
		}
		#endregion
	}
}