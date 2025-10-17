/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/


namespace XFurStudio.Utilities {

    using UnityEngine;
    using XFurStudio.Core;
    using System.Collections.Generic;

    [RequireComponent(typeof(ParticleSystem))]
    public class XFurStudioPainterParticles : MonoBehaviour {
        /// <summary>
        /// The paint data mode defines what attribute of the fur will be modified by this painter
        /// </summary>
        public XFurStudioAPI.PaintDataMode PaintDataMode = XFurStudioAPI.PaintDataMode.SnowFX;

        //public Vector4 ColorBlendMask;

        public bool SyncParticleData = true;

        /// <summary>
        /// Whether the brush currently in use should be inverted or not
        /// </summary>
        public bool InvertBrush;

        /// <summary>
        /// The opacity of the current virtual brush
        /// </summary>
        public float BrushOpacity = 1.0f;

        /// <summary>
        /// The hardness of the current virtual brush
        /// </summary>
        public float BrushHardness = 1.0f;

        /// <summary>
        /// The color of the current virtual brush
        /// </summary>
        public Color BrushColor = Color.white;


        //public Vector3 BoxSize = Vector3.one;
        /// <summary>
        /// The radius of the current brush
        /// </summary>
        public float Radius = 0.5f;

        private Vector3 prevPos;
        private int testPos;


        private ParticleSystem part;
        private ParticleSystem.Particle[] particles;
        private List<ParticleCollisionEvent> collisionEvents;

#if UNITY_EDITOR
        private string version = "4.0";

        public string Version { get { return version; } }
#endif


        private void Awake() {
            part = GetComponent<ParticleSystem>();
            collisionEvents = new List<ParticleCollisionEvent>();
            particles = new ParticleSystem.Particle[1024];
        }

        private void OnParticleCollision( GameObject other ) {

            if ( !Application.isPlaying ) {
                return;
            }

            var xfurSystem = other.transform.root.GetComponentInChildren<XFurStudioInstance>();

            if ( xfurSystem ) {

                var color = BrushColor;
                var intensity = BrushOpacity;
                var hardness = BrushHardness;

                if ( SyncParticleData ) {

                    int numParticlesAlive = part.GetParticles( particles );

                    for ( int p = 0; p < numParticlesAlive; p++ ) {

                        var tempBounds = new Bounds( particles[p].position, particles[p].GetCurrentSize3D( part ) );

                        bool collides = false;

                        foreach ( Collider c in xfurSystem.GetComponentsInChildren<Collider>() ) {
                            
                            if ( c.isTrigger )
                                continue;

                            if ( c.bounds.Intersects(tempBounds) ) {
                                collides = true;
                                break;
                            }

                        }

                        if ( !collides )
                            continue;

                        var pos = xfurSystem.MainRenderer.renderer.transform.InverseTransformPoint( particles[p].position );
                        var dir = xfurSystem.MainRenderer.renderer.transform.TransformDirection( ( pos ).normalized );
                        var pColor = particles[p].GetCurrentColor( part );

                        Radius = particles[p].GetCurrentSize( part ) * 0.5f;

                        intensity = pColor.a;

                        if ( PaintDataMode == XFurStudioAPI.PaintDataMode.FurColor ) {
                            color = pColor;
                        }


                        if ( PaintDataMode == XFurStudioAPI.PaintDataMode.FurGrooming ) {
                            for ( int i = 0; i < xfurSystem.FurDataProfiles.Length; i++ ) {
                                if ( xfurSystem.MainRenderer.isFurMaterial[i] )
                                    XFurStudioAPI.Groom( xfurSystem, i, transform.position, dir, Radius, intensity, hardness, ( transform.position - prevPos ).normalized );
                            }
                            return;
                        }

                        if ( PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask || PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask || PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask || PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask ) {
                            color = InvertBrush ? Color.black : Color.white;

                            if ( PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask ) {
                                intensity = 1.0f;
                                hardness = 1.0f;
                            }

                        }
                        else if ( PaintDataMode == XFurStudioAPI.PaintDataMode.SnowFX || PaintDataMode == XFurStudioAPI.PaintDataMode.BloodFX || PaintDataMode == XFurStudioAPI.PaintDataMode.WaterFX ) {
                            color = InvertBrush ? Color.black : Color.white;
                        }

                        for ( int i = 0; i < xfurSystem.FurDataProfiles.Length; i++ ) {
                            if ( xfurSystem.MainRenderer.isFurMaterial[i] ) {
                                XFurStudioAPI.Paint( xfurSystem, PaintDataMode, i, particles[p].position, dir, Radius, intensity, hardness, color );
                            }
                        }
                    }

                }
                else {

                    int numCollisionEvents = part.GetCollisionEvents( other, collisionEvents );

                    for ( int p = 0; p < numCollisionEvents; p++ ) {

                        var pos = xfurSystem.MainRenderer.renderer.transform.InverseTransformPoint( collisionEvents[p].intersection );
                        var dir = xfurSystem.MainRenderer.renderer.transform.TransformDirection( ( pos ).normalized );

                        Radius = part.main.startSize.Evaluate( 0.5f ) * 0.5f;

                        if ( PaintDataMode == XFurStudioAPI.PaintDataMode.FurGrooming ) {
                            for ( int i = 0; i < xfurSystem.FurDataProfiles.Length; i++ ) {
                                if ( xfurSystem.MainRenderer.isFurMaterial[i] )
                                    XFurStudioAPI.Groom( xfurSystem, i, transform.position, dir, Radius, intensity, hardness, ( transform.position - prevPos ).normalized );
                            }
                            return;
                        }

                        if ( PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask || PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask || PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask || PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask ) {
                            color = InvertBrush ? Color.black : Color.white;

                            if ( PaintDataMode == XFurStudioAPI.PaintDataMode.FurMask ) {
                                intensity = 1.0f;
                                hardness = 1.0f;
                            }

                        }
                        else if ( PaintDataMode == XFurStudioAPI.PaintDataMode.SnowFX || PaintDataMode == XFurStudioAPI.PaintDataMode.BloodFX || PaintDataMode == XFurStudioAPI.PaintDataMode.WaterFX ) {
                            color = InvertBrush ? Color.black : Color.white;
                        }

                        for ( int i = 0; i < xfurSystem.FurDataProfiles.Length; i++ ) {
                            if ( xfurSystem.MainRenderer.isFurMaterial[i] ) {
                                XFurStudioAPI.Paint( xfurSystem, PaintDataMode, i, collisionEvents[p].intersection, dir, Radius, intensity, hardness, color );
                            }
                        }

                    }
                }
            }





        }



    }

}