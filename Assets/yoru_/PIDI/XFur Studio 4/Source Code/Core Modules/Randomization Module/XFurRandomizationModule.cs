/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/

namespace XFurStudio.Modules {


    using XFurStudio.Core;
    using System.Collections.Generic;
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [System.Serializable]
    public partial class XFurRandomizationModule : XFurStudioModule {


        [System.Serializable]
        public partial class RandomizationSettings {

            /// <summary>
            /// Enables / Disables randomization for this profile
            /// </summary>
            public bool enabled;

            /// <summary>
            /// The randomziation mode : 0 values, 1 profiles, 2 both
            /// </summary>
            public int randomizationMode = 1;

            /// <summary>
            /// Whether to randomize the color map
            /// </summary>
            public bool randomizeColorMap;

            /// <summary>
            /// Whether to randomize the color variation map
            /// </summary>
            public bool randomizeColorMix;

            /// <summary>
            /// Whether to randomize the fur data maps
            /// </summary>
            public bool randomizeDataMaps;

            /// <summary>
            /// Whether to randomize the fur strands maps
            /// </summary>
            public bool randomizeFurStrands;

            /// <summary>
            /// The internal Fur Template used as a target when randomizing by values.
            /// </summary>
            public FurProfileData randomizeTo;

            /// <summary>
            /// A list of profiles to pick a random one when randomizing by profiles
            /// </summary>
            public List<FurProfileAsset> randomProfiles = new List<FurProfileAsset>();


            public RandomizationSettings() {

            }


        }

        /// <summary>
        /// Whether the randomization process should happen automatically on the Load function
        /// </summary>
        public bool randomizeOnLoad = true;

        /// <summary>
        /// The list of randomization settings for each profile.
        /// </summary>
        public List<RandomizationSettings> randomSettings = new List<RandomizationSettings>();

        /// <summary>
        /// Internal : The target version that this script is supposed to have.
        /// </summary>
        protected override Vector3Int TargetVersion { get { return new Vector3Int( 4, 0, 0 ); } }

        public override void Setup(XFurStudioInstance xfurOwner ) {
            
            _internalName = "Randomization";
            Status = ModuleStatus.Stable;
                        
            base.Setup( xfurOwner );
        }


        public XFurRandomizationModule() {

        }


        public XFurRandomizationModule( XFurRandomizationModule source ) {
            randomSettings = source.randomSettings;
            randomizeOnLoad = source.randomizeOnLoad;
            _internalName = source._internalName;
            Status = source.Status;
            _enabled = source._enabled;
            _version = source._version;
        }


        public override void MainLoop() {
        }

        public override void MainRenderLoop( MaterialPropertyBlock block, int furProfileIndex ) {
        }

        public override void Unload() {
        }

        public override void UnloadResources() {
        }


        public override void Load() {

            if ( randomizeOnLoad )
                RandomizeProfiles();

        }


        /// <summary>
        /// Triggers the randomization process based on the settings of each fur profile
        /// </summary>
        public void RandomizeProfiles() {

            if ( Application.isPlaying ) {
                for ( int i = 0; i < randomSettings.Count; i++ ) {
                    switch ( randomSettings[i].randomizationMode ) {
                        case 0 :
                            
                            float lerpValue = Random.Range( 0.0f, 1.0f );
                            var intLerp = Random.Range( 0, 2 );

                            if ( randomSettings[i].randomizeColorMap ) {
                                if ( randomSettings[i].randomizeTo.colorMap ) {
                                    if (intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[i].colorMap = randomSettings[i].randomizeTo.colorMap;
                                    }
                                }
                            }

                            if ( randomSettings[i].randomizeDataMaps ) {
                                if ( randomSettings[i].randomizeTo.furDataMap ) {
                                    if (intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[i].furDataMap = randomSettings[i].randomizeTo.furDataMap;
                                    }
                                }
                                
                                if ( randomSettings[i].randomizeTo.furGroomingMap ) {
                                    if (intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[i].furGroomingMap = randomSettings[i].randomizeTo.furGroomingMap;
                                    }
                                }
                            }

                            if ( randomSettings[i].randomizeColorMix ) {
                                if ( randomSettings[i].randomizeTo.legacyColorVariationMap ) {
                                    if (intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[i].legacyColorVariationMap = randomSettings[i].randomizeTo.legacyColorVariationMap;
                                    }
                                }

                                _xfurInstance.FurDataProfiles[i].noiseShadingTint0 = Color.Lerp( randomSettings[i].randomizeTo.noiseShadingTint0, _xfurInstance.FurDataProfiles[i].noiseShadingTint0, lerpValue );
                                _xfurInstance.FurDataProfiles[i].noiseShadingTint1 = Color.Lerp( randomSettings[i].randomizeTo.noiseShadingTint1, _xfurInstance.FurDataProfiles[i].noiseShadingTint0, lerpValue );
                                _xfurInstance.FurDataProfiles[i].noiseShadingTint2 = Color.Lerp( randomSettings[i].randomizeTo.noiseShadingTint2, _xfurInstance.FurDataProfiles[i].noiseShadingTint0, lerpValue );
                                _xfurInstance.FurDataProfiles[i].noiseShadingTint3 = Color.Lerp( randomSettings[i].randomizeTo.noiseShadingTint3, _xfurInstance.FurDataProfiles[i].noiseShadingTint0, lerpValue );

                            }

                            if ( randomSettings[i].randomizeFurStrands ) {
                                if ( randomSettings[i].randomizeTo.furStrandsAsset ) {
                                    if (intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[i].furStrandsAsset = randomSettings[i].randomizeTo.furStrandsAsset;
                                    }
                                }
                            }


                            _xfurInstance.FurDataProfiles[i].mainTint = Color.Lerp( randomSettings[i].randomizeTo.mainTint, _xfurInstance.FurDataProfiles[i].mainTint, lerpValue );
                            _xfurInstance.FurDataProfiles[i].furLength = Mathf.Lerp( randomSettings[i].randomizeTo.furLength, _xfurInstance.FurDataProfiles[i].furLength, lerpValue );
                            _xfurInstance.FurDataProfiles[i].furThickness = Mathf.Lerp( randomSettings[i].randomizeTo.furThickness, _xfurInstance.FurDataProfiles[i].furThickness, lerpValue );
                            _xfurInstance.FurDataProfiles[i].furThicknessCurve = Mathf.Lerp( randomSettings[i].randomizeTo.furThicknessCurve, _xfurInstance.FurDataProfiles[i].furThicknessCurve, lerpValue );
                            _xfurInstance.FurDataProfiles[i].selfOcclusionStrength = Mathf.Lerp( randomSettings[i].randomizeTo.selfOcclusionStrength, _xfurInstance.FurDataProfiles[i].selfOcclusionStrength, lerpValue );
                            _xfurInstance.FurDataProfiles[i].selfOcclusionCurve = Mathf.Lerp( randomSettings[i].randomizeTo.selfOcclusionCurve, _xfurInstance.FurDataProfiles[i].selfOcclusionCurve, lerpValue );
                            _xfurInstance.FurDataProfiles[i].selfOcclusionTint = Color.Lerp( randomSettings[i].randomizeTo.selfOcclusionTint, _xfurInstance.FurDataProfiles[i].selfOcclusionTint, lerpValue );
                            break;


                        case 1 :

                            if ( randomSettings[i].randomProfiles.Count > 0 ) {

                                int profile = Random.Range( 0, randomSettings[i].randomProfiles.Count );

                                if ( randomSettings[i].randomProfiles[profile] != null ) {
                                    _xfurInstance.SetFurData( i, randomSettings[i].randomProfiles[profile].FurProfileData, randomSettings[i].randomizeColorMap, randomSettings[i].randomizeDataMaps, randomSettings[i].randomizeColorMix, randomSettings[i].randomizeFurStrands );
                                }

                            }

                            break;


                        case 2:

                            if ( randomSettings[i].randomProfiles.Count > 0 ) {

                                int profile = Random.Range( 0, randomSettings[i].randomProfiles.Count );

                                lerpValue = Random.Range( 0.0f, 1.0f );
                                intLerp = Random.Range( 0, 2 );

                                if ( randomSettings[i].randomizeColorMap ) {
                                    if ( randomSettings[i].randomProfiles[profile].FurProfileData.colorMap ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[i].colorMap = randomSettings[i].randomProfiles[profile].FurProfileData.colorMap;
                                        }
                                    }
                                }

                                if ( randomSettings[i].randomizeDataMaps ) {
                                    if ( randomSettings[i].randomProfiles[profile].FurProfileData.furDataMap ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[i].furDataMap = randomSettings[i].randomProfiles[profile].FurProfileData.furDataMap;
                                        }
                                    }

                                    if ( randomSettings[i].randomProfiles[profile].FurProfileData.furGroomingMap ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[i].furGroomingMap = randomSettings[i].randomProfiles[profile].FurProfileData.furGroomingMap;
                                        }
                                    }
                                }

                                if ( randomSettings[i].randomizeColorMix ) {
                                    if ( randomSettings[i].randomProfiles[profile].FurProfileData.legacyColorVariationMap ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[i].legacyColorVariationMap = randomSettings[i].randomProfiles[profile].FurProfileData.legacyColorVariationMap;
                                        }
                                    }

                                    _xfurInstance.FurDataProfiles[i].noiseShadingTint0 = Color.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.noiseShadingTint0, _xfurInstance.FurDataProfiles[i].noiseShadingTint0, lerpValue );
                                    _xfurInstance.FurDataProfiles[i].noiseShadingTint1 = Color.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.noiseShadingTint1, _xfurInstance.FurDataProfiles[i].noiseShadingTint0, lerpValue );
                                    _xfurInstance.FurDataProfiles[i].noiseShadingTint2 = Color.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.noiseShadingTint2, _xfurInstance.FurDataProfiles[i].noiseShadingTint0, lerpValue );
                                    _xfurInstance.FurDataProfiles[i].noiseShadingTint3 = Color.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.noiseShadingTint3, _xfurInstance.FurDataProfiles[i].noiseShadingTint0, lerpValue );

                                }

                                if ( randomSettings[i].randomizeFurStrands ) {
                                    if ( randomSettings[i].randomProfiles[profile].FurProfileData.furStrandsAsset ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[i].furStrandsAsset = randomSettings[i].randomProfiles[profile].FurProfileData.furStrandsAsset;
                                        }
                                    }
                                }



                                _xfurInstance.FurDataProfiles[i].mainTint = Color.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.mainTint, _xfurInstance.FurDataProfiles[i].mainTint, lerpValue );
                                _xfurInstance.FurDataProfiles[i].furLength = Mathf.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.furLength, _xfurInstance.FurDataProfiles[i].furLength, lerpValue );
                                _xfurInstance.FurDataProfiles[i].furThickness = Mathf.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.furThickness, _xfurInstance.FurDataProfiles[i].furThickness, lerpValue );
                                _xfurInstance.FurDataProfiles[i].furThicknessCurve = Mathf.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.furThicknessCurve, _xfurInstance.FurDataProfiles[i].furThicknessCurve, lerpValue );
                                _xfurInstance.FurDataProfiles[i].selfOcclusionStrength = Mathf.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.selfOcclusionStrength, _xfurInstance.FurDataProfiles[i].selfOcclusionStrength, lerpValue );
                                _xfurInstance.FurDataProfiles[i].selfOcclusionCurve = Mathf.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.selfOcclusionCurve, _xfurInstance.FurDataProfiles[i].selfOcclusionCurve, lerpValue );
                                _xfurInstance.FurDataProfiles[i].selfOcclusionTint = Color.Lerp( randomSettings[i].randomProfiles[profile].FurProfileData.selfOcclusionTint, _xfurInstance.FurDataProfiles[i].selfOcclusionTint, lerpValue );
                            
                            }
                            
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Triggers the randomization process based on the settings of each fur profile
        /// </summary>
        public void RandomizeProfiles( int index ) {

            if ( Application.isPlaying ) {
                    switch ( randomSettings[index].randomizationMode ) {
                        case 0:

                            float lerpValue = Random.Range( 0.0f, 1.0f );
                            var intLerp = Random.Range( 0, 2 );

                            if ( randomSettings[index].randomizeColorMap ) {
                                if ( randomSettings[index].randomizeTo.colorMap ) {
                                    if ( intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[index].colorMap = randomSettings[index].randomizeTo.colorMap;
                                    }
                                }
                            }

                            if ( randomSettings[index].randomizeDataMaps ) {
                                if ( randomSettings[index].randomizeTo.furDataMap ) {
                                    if ( intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[index].furDataMap = randomSettings[index].randomizeTo.furDataMap;
                                    }
                                }

                                if ( randomSettings[index].randomizeTo.furGroomingMap ) {
                                    if ( intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[index].furGroomingMap = randomSettings[index].randomizeTo.furGroomingMap;
                                    }
                                }
                            }

                            if ( randomSettings[index].randomizeColorMix ) {
                                if ( randomSettings[index].randomizeTo.legacyColorVariationMap ) {
                                    if ( intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[index].legacyColorVariationMap = randomSettings[index].randomizeTo.legacyColorVariationMap;
                                    }
                                }

                                _xfurInstance.FurDataProfiles[index].noiseShadingTint0 = Color.Lerp( randomSettings[index].randomizeTo.noiseShadingTint0, _xfurInstance.FurDataProfiles[index].noiseShadingTint0, lerpValue );
                                _xfurInstance.FurDataProfiles[index].noiseShadingTint1 = Color.Lerp( randomSettings[index].randomizeTo.noiseShadingTint1, _xfurInstance.FurDataProfiles[index].noiseShadingTint0, lerpValue );
                                _xfurInstance.FurDataProfiles[index].noiseShadingTint2 = Color.Lerp( randomSettings[index].randomizeTo.noiseShadingTint2, _xfurInstance.FurDataProfiles[index].noiseShadingTint0, lerpValue );
                                _xfurInstance.FurDataProfiles[index].noiseShadingTint3 = Color.Lerp( randomSettings[index].randomizeTo.noiseShadingTint3, _xfurInstance.FurDataProfiles[index].noiseShadingTint0, lerpValue );

                            }

                            if ( randomSettings[index].randomizeFurStrands ) {
                                if ( randomSettings[index].randomizeTo.furStrandsAsset ) {
                                    if ( intLerp < 1 ) {
                                        _xfurInstance.FurDataProfiles[index].furStrandsAsset = randomSettings[index].randomizeTo.furStrandsAsset;
                                    }
                                }
                            }


                            _xfurInstance.FurDataProfiles[index].mainTint = Color.Lerp( randomSettings[index].randomizeTo.mainTint, _xfurInstance.FurDataProfiles[index].mainTint, lerpValue );
                            _xfurInstance.FurDataProfiles[index].furLength = Mathf.Lerp( randomSettings[index].randomizeTo.furLength, _xfurInstance.FurDataProfiles[index].furLength, lerpValue );
                            _xfurInstance.FurDataProfiles[index].furThickness = Mathf.Lerp( randomSettings[index].randomizeTo.furThickness, _xfurInstance.FurDataProfiles[index].furThickness, lerpValue );
                            _xfurInstance.FurDataProfiles[index].furThicknessCurve = Mathf.Lerp( randomSettings[index].randomizeTo.furThicknessCurve, _xfurInstance.FurDataProfiles[index].furThicknessCurve, lerpValue );
                            _xfurInstance.FurDataProfiles[index].selfOcclusionStrength = Mathf.Lerp( randomSettings[index].randomizeTo.selfOcclusionStrength, _xfurInstance.FurDataProfiles[index].selfOcclusionStrength, lerpValue );
                            _xfurInstance.FurDataProfiles[index].selfOcclusionCurve = Mathf.Lerp( randomSettings[index].randomizeTo.selfOcclusionCurve, _xfurInstance.FurDataProfiles[index].selfOcclusionCurve, lerpValue );
                            _xfurInstance.FurDataProfiles[index].selfOcclusionTint = Color.Lerp( randomSettings[index].randomizeTo.selfOcclusionTint, _xfurInstance.FurDataProfiles[index].selfOcclusionTint, lerpValue );
                            break;


                        case 1:

                            if ( randomSettings[index].randomProfiles.Count > 0 ) {

                                int profile = Random.Range( 0, randomSettings[index].randomProfiles.Count );

                                if ( randomSettings[index].randomProfiles[profile] != null ) {
                                    _xfurInstance.SetFurData( index, randomSettings[index].randomProfiles[profile].FurProfileData, randomSettings[index].randomizeColorMap, randomSettings[index].randomizeDataMaps, randomSettings[index].randomizeColorMix, randomSettings[index].randomizeFurStrands );
                                }

                            }

                            break;


                        case 2:

                            if ( randomSettings[index].randomProfiles.Count > 0 ) {

                                int profile = Random.Range( 0, randomSettings[index].randomProfiles.Count );

                                lerpValue = Random.Range( 0.0f, 1.0f );
                                intLerp = Random.Range( 0, 2 );

                                if ( randomSettings[index].randomizeColorMap ) {
                                    if ( randomSettings[index].randomProfiles[profile].FurProfileData.colorMap ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[index].colorMap = randomSettings[index].randomProfiles[profile].FurProfileData.colorMap;
                                        }
                                    }
                                }

                                if ( randomSettings[index].randomizeDataMaps ) {
                                    if ( randomSettings[index].randomProfiles[profile].FurProfileData.furDataMap ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[index].furDataMap = randomSettings[index].randomProfiles[profile].FurProfileData.furDataMap;
                                        }
                                    }

                                    if ( randomSettings[index].randomProfiles[profile].FurProfileData.furGroomingMap ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[index].furGroomingMap = randomSettings[index].randomProfiles[profile].FurProfileData.furGroomingMap;
                                        }
                                    }
                                }

                                if ( randomSettings[index].randomizeColorMix ) {
                                    if ( randomSettings[index].randomProfiles[profile].FurProfileData.legacyColorVariationMap ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[index].legacyColorVariationMap = randomSettings[index].randomProfiles[profile].FurProfileData.legacyColorVariationMap;
                                        }
                                    }

                                    _xfurInstance.FurDataProfiles[index].noiseShadingTint0 = Color.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.noiseShadingTint0, _xfurInstance.FurDataProfiles[index].noiseShadingTint0, lerpValue );
                                    _xfurInstance.FurDataProfiles[index].noiseShadingTint1 = Color.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.noiseShadingTint1, _xfurInstance.FurDataProfiles[index].noiseShadingTint0, lerpValue );
                                    _xfurInstance.FurDataProfiles[index].noiseShadingTint2 = Color.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.noiseShadingTint2, _xfurInstance.FurDataProfiles[index].noiseShadingTint0, lerpValue );
                                    _xfurInstance.FurDataProfiles[index].noiseShadingTint3 = Color.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.noiseShadingTint3, _xfurInstance.FurDataProfiles[index].noiseShadingTint0, lerpValue );

                                }

                                if ( randomSettings[index].randomizeFurStrands ) {
                                    if ( randomSettings[index].randomProfiles[profile].FurProfileData.furStrandsAsset ) {
                                        if ( intLerp < 1 ) {
                                            _xfurInstance.FurDataProfiles[index].furStrandsAsset = randomSettings[index].randomProfiles[profile].FurProfileData.furStrandsAsset;
                                        }
                                    }
                                }



                                _xfurInstance.FurDataProfiles[index].mainTint = Color.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.mainTint, _xfurInstance.FurDataProfiles[index].mainTint, lerpValue );
                                _xfurInstance.FurDataProfiles[index].furLength = Mathf.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.furLength, _xfurInstance.FurDataProfiles[index].furLength, lerpValue );
                                _xfurInstance.FurDataProfiles[index].furThickness = Mathf.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.furThickness, _xfurInstance.FurDataProfiles[index].furThickness, lerpValue );
                                _xfurInstance.FurDataProfiles[index].furThicknessCurve = Mathf.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.furThicknessCurve, _xfurInstance.FurDataProfiles[index].furThicknessCurve, lerpValue );
                                _xfurInstance.FurDataProfiles[index].selfOcclusionStrength = Mathf.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.selfOcclusionStrength, _xfurInstance.FurDataProfiles[index].selfOcclusionStrength, lerpValue );
                                _xfurInstance.FurDataProfiles[index].selfOcclusionCurve = Mathf.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.selfOcclusionCurve, _xfurInstance.FurDataProfiles[index].selfOcclusionCurve, lerpValue );
                                _xfurInstance.FurDataProfiles[index].selfOcclusionTint = Color.Lerp( randomSettings[index].randomProfiles[profile].FurProfileData.selfOcclusionTint, _xfurInstance.FurDataProfiles[index].selfOcclusionTint, lerpValue );

                            }

                            break;
                    }
                }
            
        }

#if UNITY_EDITOR

        public bool[] randomFolds;

        public override void ModuleUI( SerializedProperty property ) {

            base.ModuleUI( property );
            GUILayout.Space( 16 );

            //UnityEditor.Undo.RecordObject( this, _xfurInstance.name + _xfurInstance.GetInstanceID() + this.name );

            if ( _xfurInstance.MainRenderer.materials != null ) {

                if ( randomSettings == null || randomSettings.Count < _xfurInstance.MainRenderer.materials.Length ) {
                    randomSettings = new List<RandomizationSettings>();
                    for (int i = 0; i < _xfurInstance.MainRenderer.materials.Length; i++ ) {
                        var rndSettings = new RandomizationSettings();
                        rndSettings.randomizeTo = new FurProfileData( TargetVersion );
                        randomSettings.Add( rndSettings );
                    }
                }

                if (randomFolds == null || randomFolds.Length < randomSettings.Count ) {
                    randomFolds = new bool[randomSettings.Count];
                }


                randomizeOnLoad = EnableDisableToggle( new GUIContent( "Randomize On Start", "Applies all random settings and randomizes the profiles of this instance upon loading it (on the OnStart function)" ), randomizeOnLoad, true );

                GUILayout.Space( 16 );


                for ( int i = 0; i < _xfurInstance.MainRenderer.materials.Length; i++ ) {
                    if (_xfurInstance.MainRenderer.isFurMaterial[i])
                        randomSettings[i].enabled = EnableDisableToggle( new GUIContent( "Randomize "+_xfurInstance.MainRenderer.materials[i].name ), randomSettings[i].enabled );
                }

                GUILayout.Space( 16 );

                for (int i = 0; i < randomSettings.Count; i++ ) {
                    if (_xfurInstance.MainRenderer.isFurMaterial[i] && randomSettings[i].enabled ) {

                        if (BeginCenteredGroup(_xfurInstance.MainRenderer.materials[i].name, ref randomFolds[i] ) ) {
                            GUILayout.Space( 16 );

                            randomSettings[i].randomizationMode = PopupField( new GUIContent( "Randomization Mode", "Defines how the fur settings will be randomized\n\nRandomize Values : Each fur value is randomized between the original fur settings and the settings in the randomization module\n\nPick Random Profile : A random fur profile from a list is picked and assigned to the character\n\nRandomize Values & Profiles : Pick a random profile from a list, and randomize the fur settings between the original settings assigned to this character and the ones in the random profile" ), randomSettings[i].randomizationMode, new string[] { "Randomize Values", "Pick Random Profile", "Randomize Values and Profiles" } );

                            GUILayout.Space( 16 );

                            randomSettings[i].randomizeColorMap = EnableDisableToggle( new GUIContent( "Randomize Color Map" ), randomSettings[i].randomizeColorMap, true );
                            randomSettings[i].randomizeDataMaps = EnableDisableToggle( new GUIContent( "Randomize Data Maps" ), randomSettings[i].randomizeDataMaps, true );
                            randomSettings[i].randomizeColorMix = EnableDisableToggle( new GUIContent( "Randomize Color Mixing" ), randomSettings[i].randomizeColorMix, true );
                            randomSettings[i].randomizeFurStrands = EnableDisableToggle( new GUIContent( "Randomize Strands Asset" ), randomSettings[i].randomizeFurStrands, true );

                            GUILayout.Space( 16 );

                            switch ( randomSettings[i].randomizationMode ) {

                                case 0:

                                    if ( randomSettings[i].randomizeFurStrands ) {
                                            randomSettings[i].randomizeTo.furStrandsAsset = ObjectField<XFurStudioStrandsAsset>( new GUIContent( "Fur Strands Asset", "The texture map used to generate the fur strands for this fur profile" ), randomSettings[i].randomizeTo.furStrandsAsset );
                                    }
                                    
                                    if ( randomSettings[i].randomizeColorMap ) {
                                        randomSettings[i].randomizeTo.colorMap = ObjectField<Texture>( new GUIContent( "Fur Color Map", "The texture that controls the color / albedo applied over the whole fur surface" ), randomSettings[i].randomizeTo.colorMap );
                                    }

                                    if ( randomSettings[i].randomizeDataMaps ) {
                                        _xfurInstance.FurDataProfiles[i].furDataMap = ObjectField<Texture>( new GUIContent( "Fur Data Map", "The texture that controls the parameters of the fur :\n\n R = fur mask\n G = length\n B = occlusion\n A = thickness" ), _xfurInstance.FurDataProfiles[i].furDataMap );
                                        _xfurInstance.FurDataProfiles[i].furGroomingMap = ObjectField<Texture>( new GUIContent( "Fur Grooming Map", "The texture that controls the direction of the fur :\n\n RGB = fur direction\n A = stiffness" ), _xfurInstance.FurDataProfiles[i].furGroomingMap );
                                        
                                    }

                                    if ( randomSettings[i].randomizeColorMix ) {
                                        GUILayout.Space( 8 );
                                        randomSettings[i].randomizeTo.legacyColorVariationMap = ObjectField<Texture>( new GUIContent( "Fur Color Variation", "The texture that controls four additional coloring variations to be applied over the fur, either all four to the whole fur or two to the undercoat and two to the overcoat by using the four color channels." ), randomSettings[i].randomizeTo.legacyColorVariationMap );
                                        
                                        if ( randomSettings[i].randomizeTo.legacyColorVariationMap ) {
                                            GUILayout.Space( 8 );
                                            randomSettings[i].randomizeTo.noiseShadingTint0 = EditorGUILayout.ColorField( new GUIContent( "Fur Color A", "The fur color to be applied on the red channel of the Color Variation map" ), randomSettings[i].randomizeTo.noiseShadingTint0 );
                                            randomSettings[i].randomizeTo.noiseShadingTint1 = EditorGUILayout.ColorField( new GUIContent( "Fur Color B", "The fur color to be applied on the green channel of the Color Variation map" ), randomSettings[i].randomizeTo.noiseShadingTint1 );
                                            randomSettings[i].randomizeTo.noiseShadingTint2 = EditorGUILayout.ColorField( new GUIContent( "Fur Color C", "The fur color to be applied on the blue channel of the Color Variation map" ), randomSettings[i].randomizeTo.noiseShadingTint2 );
                                            randomSettings[i].randomizeTo.noiseShadingTint3 = EditorGUILayout.ColorField( new GUIContent( "Fur Color D", "The fur color to be applied on the alpha channel of the Color Variation map" ), randomSettings[i].randomizeTo.noiseShadingTint3 );
                                        }
                                    }

                                    GUILayout.Space( 16 );

                                    randomSettings[i].randomizeTo.furLength = EditorGUILayout.Slider( new GUIContent( "Fur Length", "The maximum overall length of the fur. This will be multiplied by the actual fur profile length and the length painted in XFur Studio™ - Designer" ), randomSettings[i].randomizeTo.furLength, 0, 1 );

                                    GUILayout.Space( 8 );
                                    randomSettings[i].randomizeTo.furThickness = EditorGUILayout.Slider( new GUIContent( "Fur Thickness", "The maximum overall thickness of the fur. This will be multiplied by the actual fur profile thickness and the thickness painted in XFur Studio™ - Designer" ), randomSettings[i].randomizeTo.furThickness, 0, 1 );
                                    randomSettings[i].randomizeTo.furThicknessCurve = EditorGUILayout.Slider( new GUIContent( "Thickness Curve", "How the fur strands' thickness bias will change from the root to the top of each strand" ), randomSettings[i].randomizeTo.furThicknessCurve, 0, 1 );
                                    GUILayout.Space( 8 );

                                    randomSettings[i].randomizeTo.selfOcclusionTint = EditorGUILayout.ColorField( new GUIContent( "Occlusion Tint" ), randomSettings[i].randomizeTo.selfOcclusionTint );
                                    randomSettings[i].randomizeTo.selfOcclusionStrength = EditorGUILayout.Slider( new GUIContent( "Fur Occlusion / Shadowing", "The shadowing applied over the surface of the fur strands as a simple occlusion pass. Multiplied by the per-profile occlusion value and the one painted through XFur Studio™ - Designer" ), randomSettings[i].randomizeTo.selfOcclusionStrength, 0, 1 );
                                    randomSettings[i].randomizeTo.selfOcclusionCurve = EditorGUILayout.Slider( new GUIContent( "Fur Occlusion Curve", "How the shadowing / occlusion of the fur will go from the root to the tip of each strand" ), randomSettings[i].randomizeTo.selfOcclusionCurve, 0, 1 );

                                    GUILayout.Space( 16 );

                                    if (CenteredButton( "Copy from current Settings", 256 ) ) {
                                        _xfurInstance.GetFurData( i, out randomSettings[i].randomizeTo );
                                    }

                                    GUILayout.Space( 24 );

                                    break;

                                default :

                                    for ( int p = 0; p < randomSettings[i].randomProfiles.Count; p++ ) {
                                        GUILayout.BeginHorizontal();

                                        randomSettings[i].randomProfiles[p] = ObjectField<FurProfileAsset>( new GUIContent( "Fur Profile " + p ), randomSettings[i].randomProfiles[p] );

                                        if ( StandardButton("X", 24 ) ) {
                                            randomSettings[i].randomProfiles.RemoveAt( p );
                                            GUILayout.EndHorizontal();
                                            break;
                                        }

                                        GUILayout.EndHorizontal();
                                    }

                                    GUILayout.Space( 16 );

                                    if ( CenteredButton("Add new Random Profile", 256 ) ) {
                                        randomSettings[i].randomProfiles.Add( null );
                                    }

                                    GUILayout.Space( 16 );

                                    break;

                            }

                        }
                        EndCenteredGroup();

                    }
                }


            
            }

            GUILayout.Space( 16 );
        }

        public override void UpdateModule() {
            _internalName = "Randomization";
            Status = ModuleStatus.Stable;
            _version = TargetVersion;
        }

#endif

    }

}