using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch; 
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using Unity.VisualScripting;
using System;
using TMPro; 

public class ChessGameIntitializer : MonoBehaviour
{
    [SerializeField]
    private GameObject chessboardPrefab;

    [SerializeField]
    private GameObject whiteScoreboardPrefab;

    [SerializeField]
    private GameObject blackScoreboardPrefab;

    [SerializeField]
    private ARPlaneManager arPlaneManager;

    private ARRaycastManager arRaycastManager;

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private bool chessboardPlaced = false;

    [SerializeField]
    private float verticalOffset = 0.1f;

    [SerializeField]
    private float gridSize = 8f;

    [SerializeField]
    private Material whiteMaterial;
    
    [SerializeField]
    private Material blackMaterial;

    [Header("WhitePieces")]
    [SerializeField]
    private GameObject[] whitePieces;
    [SerializeField]
    private GameObject whitePawnPrefab;

    [Header("BlackPieces")]
    [SerializeField]
    private GameObject[] blackPieces;
    [SerializeField] 
    private GameObject blackPawnPrefab;


    private static GameObject[,] BOARDSTATE = new GameObject[8, 8];

    public static bool isBoardInitialized = false;

    public static int whiteScore = 0;
    public static int blackScore = 0;

    private static TextMeshPro blackScoreText;
    private static TextMeshPro whiteScoreText;

    private void Awake()
    {

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                BOARDSTATE[row, col] = null;
            }
        }

        arRaycastManager = GetComponent<ARRaycastManager>();
        if (arRaycastManager == null)
        {
            // debugText.tex = "Component not found"
        }
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        PlaceChessboard();
    }

    private void PlaceChessboard()
    {
        if (chessboardPlaced) return;

        foreach (var touch in Touch.activeTouches)
        {
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                Vector2 touchPosition = touch.screenPosition;

                if (arRaycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
                {
                    Pose hitPose = hits[0].pose;
                    
                    Vector3 spawnScale = new Vector3(0.1f, 0.1f, 0.1f);

                    Quaternion rotationAdjustment = Quaternion.Euler(0, 180, 0);
                    Quaternion finalRotation = hitPose.rotation * rotationAdjustment;

                    GameObject chessboard = Instantiate(chessboardPrefab, hitPose.position, finalRotation);

                    chessboard.transform.localScale = spawnScale;

                    GameObject whiteCaptureBoard = Instantiate(whiteScoreboardPrefab, chessboard.transform);
                    whiteCaptureBoard.transform.localPosition = new Vector3(3.5f, 0.08f, 11f);
                    whiteCaptureBoard.transform.localScale = new Vector3(10f, 5f, 10f);

                    whiteScoreText = whiteCaptureBoard.GetComponentInChildren<TextMeshPro>();
                    whiteScoreText.text = "Score: " + whiteScore;

                    GameObject blackCaptureBoard = Instantiate(blackScoreboardPrefab, chessboard.transform);
                    blackCaptureBoard.transform.localPosition = new Vector3(3.5f, 0.08f, -4f);
                    blackCaptureBoard.transform.localScale = new Vector3(10f, 5f, 10f);

                    blackScoreText = blackCaptureBoard.GetComponentInChildren<TextMeshPro>();
                    blackScoreText.text = "Score: " + blackScore;

                    chessboardPlaced = true;

                    DisablePlaneTracking();

                    GenerateChessboardGrid(chessboard, gridSize);

                    SpawnChessPieces(chessboard);
                }
            }
        }
    }

    private void DisablePlaneTracking()
    {
        if (arPlaneManager != null)
        {
            arPlaneManager.enabled = false;

            foreach (var plane in arPlaneManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
        }
    }

    private void GenerateChessboardGrid(GameObject chessboard, float gridDimension)
    {
        float tileSize = gridDimension / 8f;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                GameObject square = GameObject.CreatePrimitive(PrimitiveType.Quad);
                square.transform.SetParent(chessboard.transform, false);

                float xPos = (col * tileSize) + (tileSize / 2f) - 0.5f;
                float zPos = (row * tileSize) + (tileSize / 2f) - 0.5f;
                square.transform.localPosition = new Vector3(xPos, verticalOffset, zPos);

                square.transform.localRotation = Quaternion.Euler(90, 0, 0);

                square.transform.localScale = new Vector3(tileSize, tileSize, 1);

                square.tag = "TILE";
                square.AddComponent<BoxCollider>();

                MeshRenderer renderer = square.GetComponent<MeshRenderer>();
                if ((row + col) % 2 == 0)
                    renderer.material = blackMaterial;
                else
                    renderer.material = whiteMaterial;
            }
        }
    }

    private void SpawnChessPieces(GameObject chessboard)
    {
        // White Pieces
        for (int i = 0; i <= 7; i++)
        {
            GameObject chessPiece = Instantiate(whitePieces[i], chessboard.transform);
            Vector3 spawnPosition = new Vector3(i, 0f, 7f);
            chessPiece.transform.localPosition = spawnPosition;

            if (i == 6 || i == 1)
            {
                Quaternion rotationAdjustment = Quaternion.Euler(0, 180, 0);
                chessPiece.transform.localRotation = rotationAdjustment;
            }

            if (i == 2 || i == 4)
            {
                Quaternion rotationAdjustment = Quaternion.Euler(0, 90, 0);
                chessPiece.transform.localRotation = rotationAdjustment;
            }

            chessPiece.tag = "PIECE";
            chessPiece.AddComponent<BoxCollider>();

            switch (i)
            {
                case 7: case 0:
                    chessPiece.name = "WHITE_ROOK";
                    break;
                case 6: case 1:
                    chessPiece.name = "WHITE_KNIGHT";
                    break;
                case 5: case 2:
                    chessPiece.name = "WHITE_BISHOP";
                    break;
                case 4:
                    chessPiece.name = "WHITE_QUEEN";
                    break;
                case 3:
                    chessPiece.name = "WHITE_KING";
                    break;
            }

            BOARDSTATE[7, 7 - i] = chessPiece;
        }

        for (int i = 0; i <= 7; i++)
        {
            GameObject chessPiece = Instantiate(whitePawnPrefab, chessboard.transform);
            Vector3 spawnPosition = new Vector3(i, 0f, 6f);
            chessPiece.transform.localPosition = spawnPosition;

            chessPiece.name = "WHITE_PAWN";
            chessPiece.tag = "PIECE";
            chessPiece.AddComponent<BoxCollider>();

            BOARDSTATE[6, 7 - i] = chessPiece;
        }


        // Black Pieces
        for (int i = 0; i <= 7; i++)
        {
            GameObject chessPiece = Instantiate(blackPieces[i], chessboard.transform);
            Vector3 spawnPosition = new Vector3(i, 0f, 0f);
            chessPiece.transform.localPosition = spawnPosition;
            chessPiece.tag = "PIECE";
            chessPiece.AddComponent<BoxCollider>();

            switch (i)
            {
                case 7:
                case 0:
                    chessPiece.name = "BLACK_ROOK";
                    break;
                case 6:
                case 1:
                    chessPiece.name = "BLACK_KNIGHT";
                    break;
                case 5:
                case 2:
                    chessPiece.name = "BLACK_BISHOP";
                    break;
                case 4:
                    chessPiece.name = "BLACK_QUEEN";
                    break;
                case 3:
                    chessPiece.name = "BLACK_KING";
                    break;
            }

            BOARDSTATE[0, 7 - i] = chessPiece;
        }

        for (int i = 0; i <= 7; i++)
        {
            GameObject chessPiece = Instantiate(blackPawnPrefab, chessboard.transform);
            Vector3 spawnPosition = new Vector3(i, 0f, 1f);
            chessPiece.transform.localPosition = spawnPosition;
            
            chessPiece.name = "BLACK_PAWN";
            chessPiece.tag = "PIECE";
            chessPiece.AddComponent<BoxCollider>();

            BOARDSTATE[1, 7 - i] = chessPiece;
        }

        isBoardInitialized = true;     
    }

    public static GameObject[,] GetBoardState()
    {
        return BOARDSTATE;
    }

    public static void UpdateScoreWhite()
    {
        whiteScoreText.text = "Score: " + whiteScore;
    }

    public static void UpdateScoreBlack()
    {
        blackScoreText.text = "Score: " + blackScore;
    }
}
