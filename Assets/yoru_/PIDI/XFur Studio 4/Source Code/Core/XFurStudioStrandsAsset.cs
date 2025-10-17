/*

XFur Studio™, by Irreverent Software™
Copyright© 2018-2025, Jorge Pinal Negrete. All Rights Reserved.

*/


namespace XFurStudio.Core {

    using System.Collections.Generic;
    using UnityEngine;



    [CreateAssetMenu(fileName = "XFur Studio - Fur Strands Asset", menuName = "XFur Studio 4/New Fur Strands Asset")]
    public partial class XFurStudioStrandsAsset : ScriptableObject {


        //NEW XFUR STRANDS CODE HERE :
        [System.Serializable]
        public class XFurStrandShape {

            public Texture2D customShape;

#if UNITY_EDITOR
            public Texture2D previewShape;
#endif

            public float horizontalSize = 1.0f;

            public float verticalSize = 1.0f;

            public float gradient = 0.5f;


            public void GenerateStrandShape( Texture2D shapeTex ) {

                for ( int y = 0; y < shapeTex.width; ++y ) {
                    for ( int x = 0; x < shapeTex.width; ++x ) {
                        var dist = Vector2.Distance( new Vector2( (int)( shapeTex.width * 0.5f ), (int)( shapeTex.width * 0.5f ) ), new Vector2( x, y ) );
                        var gradPoint = Mathf.Pow( Mathf.Clamp01( 1.0f - ( dist / ( shapeTex.width * 0.5f ) ) ), 0.5f + ( 2.5f * gradient ) );
                        shapeTex.SetPixel( x, y, new Color( gradPoint, gradPoint, gradPoint, 1 ) );
                    }
                }


                shapeTex.Apply();

                var tempT = new Texture2D( (int)( shapeTex.width * horizontalSize ), (int)( shapeTex.height * verticalSize ) );

                var colors = new Color[tempT.width * tempT.height];
                for ( int i = 0; i < colors.Length; i++ ) {
                    colors[i] = Color.black;
                }

                tempT.SetPixels( colors );
                tempT.Apply();

                for ( int y = 0; y < shapeTex.width; ++y ) {
                    for ( int x = 0; x < shapeTex.width; ++x ) {
                        var srcColor = shapeTex.GetPixel( x, y );
                        var tColor = tempT.GetPixel( (int)Mathf.Clamp( ( x * horizontalSize ), 0, tempT.width ), (int)Mathf.Clamp( ( y * verticalSize ), 0, tempT.height ) );

                        if ( tColor != Color.black ) {
                            tColor = Color.Lerp( srcColor, tColor, 0.5f );
                        }
                        else {
                            tColor = srcColor;
                        }
                        
                        tempT.SetPixel( (int)Mathf.Clamp( ( x * horizontalSize ), 0, tempT.width ), (int)Mathf.Clamp( ( y * verticalSize ), 0, tempT.height ), tColor );
                    }
                }

                tempT.Apply();

                var offsetX = (int)( ( shapeTex.width - tempT.width ) / 2 );
                var offsetY = (int)( ( shapeTex.height - tempT.height ) / 2 );

                for ( int y = 0; y < shapeTex.width; ++y ) {
                    for ( int x = 0; x < shapeTex.width; ++x ) {
                        if ( x > offsetX && x < shapeTex.width - offsetX ) {
                            if ( y > offsetY && y < shapeTex.height - offsetY ) {
                                shapeTex.SetPixel( x, y, tempT.GetPixel( (int)Mathf.Clamp( ( x - offsetX ), 0, tempT.width ), (int)Mathf.Clamp( ( y - offsetY ), 0, tempT.height ) ) );
                            }
                            else {
                                shapeTex.SetPixel( x, y, Color.black );
                            }
                        }
                        else {
                            shapeTex.SetPixel( x, y, Color.black );
                        }
                    }
                }

                shapeTex.Apply();

                DestroyImmediate( tempT );

            }

        }


        public enum XFurStrandsMethod { ProcedurallyGenerated, CustomTexture };

        public XFurStrandsMethod xfurStrandsMethod;

        public Texture2D CustomStrandsTexture;

        [SerializeField] protected Texture2D proceduralStrandsTexture;

        public XFurStrandShape strandShapeA = new XFurStrandShape();

        public XFurStrandShape strandShapeB = new XFurStrandShape();


        #region Fur Strands Generator Properties


        public float firstPassDensity = 0.85f;

        public float secondPassDensity = 0.5f;

        public float firstPassSize = 0.25f;

        public float secondPassSize = 0.35f;

        public float firstPassVariation = 1.0f;

        public float secondPassVariation = 1.0f;

        public bool randomizeRotations = false;

        public bool generateMips = true;

        #endregion


        //OLD XFUR STRANDS CODE HERE :



        #region Fur Strand Shape Properties

        public float roundness = 1.0f;

        public float horizontalAspect = 1.0f;

        public float verticalAspect = 1.0f;

        public float gradientStrength = 0.5f;

        #endregion


        #region Fur Strand Distribution

        public float strandsDensity = 0.5f;

        #endregion


        [Range(4,64)] public float perlinNoiseScale = 4.0f;
        Vector2 perlinNoiseOffset;

        public string Version { get { return "3.2.0"; } }



        public Texture2D FurStrandsTexture { get { 
                switch( xfurStrandsMethod ) {
                    default:
                        return proceduralStrandsTexture;

                    case XFurStrandsMethod.ProcedurallyGenerated:
                        return proceduralStrandsTexture ? proceduralStrandsTexture : Texture2D.blackTexture;

                    case XFurStrandsMethod.CustomTexture:
                        return CustomStrandsTexture ? CustomStrandsTexture : Texture2D.blackTexture;
                }
            }
        }



        private List<Vector2> GeneratePoissonPoints(float radius) {
            List<Vector2> poissonPoints = new List<Vector2>();
            List<Vector2> spawnPoints = new List<Vector2>();


            float cellSize = radius / Mathf.Sqrt( 2 );

            int[,] poissonGrid = new int[Mathf.CeilToInt( 512 / cellSize ), Mathf.CeilToInt( 512 / cellSize )];

            spawnPoints.Add( new Vector2( 256, 256 ) );

            while ( spawnPoints.Count > 0 ) {
                int spawnIndex = Random.Range( 0, spawnPoints.Count );
                Vector2 spawnCenter = spawnPoints[spawnIndex];
                bool pointAccepted = false;


                for ( int i = 0; i < 30; ++i ) {

                    float angle = Random.value * Mathf.PI * 2;
                    Vector2 dir = new Vector2( Mathf.Sin( angle ), Mathf.Cos( angle ) );
                    Vector2 candidate = spawnCenter + dir * Random.Range( radius, 2 * radius );

                    if ( IsValidPoissonPoint( candidate, radius, cellSize, poissonPoints, poissonGrid ) ) {
                        poissonPoints.Add( candidate );
                        spawnPoints.Add( candidate );
                        poissonGrid[(int)(candidate.x / cellSize), (int)(candidate.y / cellSize)] = poissonPoints.Count;
                        pointAccepted = true;
                        break;
                    }

                }

                if ( !pointAccepted ) {
                    spawnPoints.RemoveAt( spawnIndex );
                }

            }

            return poissonPoints;

        }



        private int LoopValue( int value, int limit ) {

            if (value >= limit ) {
                return value - limit;
            }
            else if (value < 0 ) {
                return limit + value;
            }
            else {
                return value;
            }

        }



        void GeneratePerlinNoise() {

            if ( !proceduralStrandsTexture ) {
                return;
            }
            else {

                for (int x = 0; x < 512; x++ ) {
                    for (int y = 0; y < 512; y++ ) {

                        float xCoord = (float)x / 512 * perlinNoiseScale + perlinNoiseOffset.x;
                        float yCoord = (float)y / 512 * perlinNoiseScale + perlinNoiseOffset.y;

                        float noise = Mathf.PerlinNoise( xCoord, yCoord );

                        Color original = proceduralStrandsTexture.GetPixel( x, y );

                        proceduralStrandsTexture.SetPixel( x, y, new Color( original.r,original.g,noise) );

                    }
                }

            }

        }




        public void PoissonStrandsGenerator(  ) {

            perlinNoiseOffset = new Vector2( Random.Range( -100.0f, 100.0f), Random.Range(-100.0f,100.0f) );

            if ( !proceduralStrandsTexture ) {
                proceduralStrandsTexture = new Texture2D( 512, 512, TextureFormat.RGBA32, generateMips );
                proceduralStrandsTexture.name = "Fur Strands Output";
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.AddObjectToAsset( proceduralStrandsTexture, this );
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            else {
                DestroyImmediate(proceduralStrandsTexture, true);
                proceduralStrandsTexture = new Texture2D( 512, 512, TextureFormat.RGBA32, generateMips );
                proceduralStrandsTexture.name = "Fur Strands Output";
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.AddObjectToAsset( proceduralStrandsTexture, this );
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
#endif
            }

            var cols = new Color[512 * 512];
            for (int c = 0; c < 512*512; ++c ) {
                cols[c] = Color.black;
            }

            proceduralStrandsTexture.SetPixels( cols );
            proceduralStrandsTexture.Apply();


            var baseGradientA = strandShapeA.gradient;
            var baseGradientB = strandShapeB.gradient;

            float radius = Mathf.Lerp( 512.0f, 16.0f, firstPassDensity );

            var poissonPoints = GeneratePoissonPoints( radius );

            foreach ( Vector2 point in poissonPoints ) {
                
                var rndScale = Mathf.Clamp( (int)( Random.Range( 84 * firstPassVariation, 84 ) * firstPassSize ), 16, 84 );

                var tempT = new Texture2D( rndScale, rndScale, TextureFormat.RGBA32, false );

                strandShapeA.gradient = Mathf.Clamp01( Random.Range( baseGradientA * ( 1.0f - ( 0.35f * firstPassVariation ) ), baseGradientA * ( 1.0f + ( 0.35f * firstPassVariation ) ) ) );

                strandShapeA.GenerateStrandShape( tempT );


                int startX = LoopValue(Mathf.RoundToInt(point.x - (rndScale / 2)), 512);
                int startY = LoopValue(Mathf.RoundToInt(point.y - (rndScale / 2)), 512);

                for (int y = 0; y < rndScale; ++y ) {
                    for (int x = 0; x < rndScale; ++x ) {
                        var pixX = LoopValue( startX + x, 512 );
                        var pixY = LoopValue( startY + y, 512 );
                        var tempCol = new Color( Mathf.Lerp( tempT.GetPixel( x, y ).r, proceduralStrandsTexture.GetPixel( pixX, pixY ).r, 1-tempT.GetPixel( x, y ).r ),0,0,1);
                        proceduralStrandsTexture.SetPixel( pixX, pixY, tempCol );
                    }
                }

                DestroyImmediate( tempT );
            }

            radius = Mathf.Lerp( 512.0f, 16.0f, secondPassDensity );
            poissonPoints = GeneratePoissonPoints( radius );


            foreach ( Vector2 point in poissonPoints ) {

                var rndScale = Mathf.Clamp( (int)( Random.Range( 84 * secondPassVariation, 84 ) * secondPassSize ), 16, 84 );

                var tempT = new Texture2D( rndScale, rndScale, TextureFormat.RGBA32, false );

                strandShapeB.gradient = Mathf.Clamp01( Random.Range( baseGradientB * ( 1.0f - (0.35f * secondPassVariation ) ), baseGradientB * ( 1.0f + ( 0.35f * secondPassVariation ) ) ) );

                strandShapeB.GenerateStrandShape( tempT ); 

                int startX = LoopValue( Mathf.RoundToInt( point.x - (rndScale / 2) ), 512 );
                int startY = LoopValue( Mathf.RoundToInt( point.y - (rndScale / 2) ), 512 );

                for ( int y = 0; y < rndScale; ++y ) {
                    for ( int x = 0; x < rndScale; ++x ) {
                        var pixX = LoopValue( startX + x, 512 );
                        var pixY = LoopValue( startY + y, 512 );
                        var tempCol = new Color( proceduralStrandsTexture.GetPixel(pixX,pixY).r, Mathf.Lerp( tempT.GetPixel( x, y ).r, proceduralStrandsTexture.GetPixel( pixX, pixY ).g, 1 - tempT.GetPixel( x, y ).r ), 0, 1 );
                        proceduralStrandsTexture.SetPixel( pixX, pixY, tempCol );
                    }
                }
                DestroyImmediate( tempT );

            }


            GeneratePerlinNoise();

            proceduralStrandsTexture.Apply();
            proceduralStrandsTexture.wrapMode = TextureWrapMode.Repeat;
            proceduralStrandsTexture.anisoLevel = 16;

        }

        private bool IsValidPoissonPoint( Vector2 point, float radius, float cellSize, List<Vector2> points, int[,] grid ) {

            if ( point.x >= 0 && point.x < 512 && point.y >= 0 && point.y < 512 ) {
                int cellX = (int)(point.x / cellSize);
                int cellY = (int)(point.y / cellSize);
                int searchStartX = Mathf.Max( 0, cellX - 2 );
                int searchEndX = Mathf.Min( cellX + 2, grid.GetLength( 0 ) - 1 );
                int searchStartY = Mathf.Max( 0, cellY - 2 );
                int searchEndY = Mathf.Min( cellY + 2, grid.GetLength( 1 ) - 1 );

                for ( int x = searchStartX; x <= searchEndX; x++ ) {
                    for ( int y = searchStartY; y <= searchEndY; y++ ) {
                        int pointIndex = grid[x, y] - 1;
                        if ( pointIndex != -1 ) {
                            float sqrDst = (point - points[pointIndex]).sqrMagnitude;
                            if ( sqrDst < radius * radius ) {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        } 


    }
    

}