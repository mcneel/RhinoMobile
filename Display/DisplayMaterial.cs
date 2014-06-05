//
// DisplayMaterial.cs
// RhinoMobile.Touch
//
// Created by dan (dan@mcneel.com) on 5/20/2014
// Copyright 2014 Robert McNeel & Associates.  All rights reserved.
//
using System;
using Rhino.DocObjects;

namespace RhinoMobile.Display
{
	public class DisplayMaterial
	{
		#region properties
		public int RuntimeId { get; private set; }

		public float Shine { get; private set; }

		public double Transparency { get; private set; }

		public float Alpha { get; private set; }

		public float[] SpecularColor { get; private set; }

		public float[] AmbientColor { get; set; }

		public float[] DiffuseColor { get; private set; }

		public float[] EmissionColor {get; private set; }
		#endregion

		#region constructors and disposal
		/// <summary>
		/// Only use this argument-less constructor to set an instance to an unknown material.
		/// </summary>
		public DisplayMaterial ()
		{
			RuntimeId = Int32.MinValue; //UNSET
		}

		public DisplayMaterial (Material material, int materialIndex)
		{
			RuntimeId = materialIndex;

			Transparency = material.Transparency;
			Alpha = (float)(1.0 - material.Transparency);
			Shine = (float)(128.0 * (material.Shine / Material.MaxShine));

			var specular = material.SpecularColor;
			float[] spec = { specular.R / 255.0f, specular.G / 255.0f, specular.B / 255.0f, specular.A / 255.0f };
			SpecularColor = spec;

			var ambient = material.AmbientColor;
			float[] ambi = { ambient.R / 255.0f, ambient.G / 255.0f, ambient.B / 255.0f, ambient.A / 255.0f };
			AmbientColor = ambi;

			var diffuse = material.DiffuseColor;
			float[] diff = { diffuse.R / 255.0f, diffuse.G / 255.0f, diffuse.B / 255.0f, Alpha };
			DiffuseColor = diff;

			var emission = material.EmissionColor;
			float[] emmi = { emission.R / 255.0f, emission.G / 255.0f, emission.B / 255.0f, 1.0f };
			EmissionColor = emmi;
		}
		#endregion
	}
}

