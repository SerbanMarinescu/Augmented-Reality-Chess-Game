using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

enum Player
{
    WHITE,
    BLACK
}

public class GameController : MonoBehaviour
{
    private GameObject[,] boardState;

    private bool isAlreadyInitialized = false;

    private GameObject selectedPiece;
    private Vector3 originalPosition;

    private float distance;
    private Vector3 touchOffset;
    private float fixedY;

    private int whiteCapturedPawns = 0;
    private int whiteCapturedPieces = 0;
    private int blackCapturedPawns = 0;
    private int blackCapturedPieces = 0;

    private Player currentPlayer;

    private float pulseSpeed = 3f;
    private float alphaMin = 0.3f, alphaMax = 1f;
    private float t = 0f;

    private bool isGameWon = false;

    [SerializeField] 
    private GameObject gameOverPanel;  
    [SerializeField] 
    private TextMeshProUGUI gameOverTitle;
    [SerializeField]
    private TextMeshProUGUI finalScoreWhite;
    [SerializeField]
    private TextMeshProUGUI finalScoreBlack;

    [SerializeField] 
    private UnityEngine.UI.Image screenBorderImage;

    [SerializeField]
    private GameObject explosionPrefab;

    [SerializeField]
    private GameObject moveSoundEffect;


    [SerializeField]
    private ARTrackedImageManager trackedImageManager;

    [SerializeField]
    private TextMeshProUGUI spellDetectedText;

    private bool wasMoveMade = false;
    private bool ignoreMovementThroughPieces = false;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();

        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            ProcessTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            ProcessTrackedImage(trackedImage);
        }
    }

    private void ProcessTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            string markerName = trackedImage.referenceImage.name;

            switch (markerName)
            {
                case "Skip":
                    SkipOpponentsTurnSpell();
                    break;
                case "Freeze":
                    FreezeSpell();
                    break;
                case "Ghost":
                    GhostMovementSpell();
                    break;
                default:
                    break;
            }
        }
    }

    void Start()
    {
        currentPlayer = Player.WHITE;
        screenBorderImage.enabled = false;
    }

    void Update()
    {

        InitializeGame();

        PulsatingImageBorderAnimation();

        if (isAlreadyInitialized && !isGameWon)
        {
            DetectTouches();
        }
    }

    private void InitializeGame()
    {
        if (isAlreadyInitialized) return;

        if (ChessGameIntitializer.isBoardInitialized)
        {
            boardState = ChessGameIntitializer.GetBoardState();
            screenBorderImage.enabled = true;
            isAlreadyInitialized = true;
        }
    }

    private void DetectTouches()
    {
        foreach (var touch in Touch.activeTouches)
        {
            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    OnTouchBegan(touch.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                    OnTouchMoved(touch.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    OnTouchEnded(touch.screenPosition);
                    break;
            }
        }
    }

    private void OnTouchBegan(Vector2 position)
    {
        Ray ray = Camera.main.ScreenPointToRay(position);
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {

            if (hit.collider.CompareTag("PIECE"))
            {

                if(!hit.collider.gameObject.name.Contains(currentPlayer.ToString()))
                {
                    return;
                }

                selectedPiece = hit.collider.gameObject;
                originalPosition = selectedPiece.transform.localPosition;

                distance = Vector3.Distance(Camera.main.transform.position, selectedPiece.transform.position);
                Vector3 worldPoint = Camera.main.ScreenToWorldPoint(new Vector3(position.x, position.y, distance));
                touchOffset = selectedPiece.transform.position - worldPoint;
                fixedY = selectedPiece.transform.position.y;
            }
        }
    }

    private void OnTouchMoved(Vector2 position)
    {

        Vector3 worldPoint = Camera.main.ScreenToWorldPoint(new Vector3(position.x, position.y, distance));
        Vector3 newPos = worldPoint + touchOffset;
        newPos.y = fixedY;
        selectedPiece.transform.position = newPos;
    }

    private void OnTouchEnded(Vector2 position)
    {

        Vector3 currentPos = selectedPiece.transform.localPosition;

        int snappedX = Mathf.RoundToInt(currentPos.x);
        int snappedZ = Mathf.RoundToInt(currentPos.z);

        Vector3 snappedPosition = new Vector3(snappedX, currentPos.y, snappedZ);

        bool isMoveValid = IsMoveValid(snappedPosition);

        if(isMoveValid)
        {
            selectedPiece.transform.localPosition = snappedPosition;
            UpdateBoardState(snappedPosition);
            UpdateCurrentPlayer();
        }
        else
        {
            selectedPiece.transform.localPosition = originalPosition;
            selectedPiece = null;
        }   
    }

    private bool IsMoveCapture(Vector3 position)
    {
        Vector2Int indices = GlobalToBoardState((int)position.x, (int)position.z);


        if(boardState[indices.x, indices.y] != null)
        {
            if (!boardState[indices.x, indices.y].name.Contains(currentPlayer.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateCurrentPlayer()
    {
        if(currentPlayer == Player.WHITE)
        {
            currentPlayer = Player.BLACK;
            screenBorderImage.color = Color.black;
        }
        else
        {
            currentPlayer = Player.WHITE;
            screenBorderImage.color = Color.white;
        }
    }

    private bool IsMoveValid(Vector3 position)
    {

        // Out of board bounds
        if(position.x > 7 || position.x < 0 || position.z > 7 || position.z < 0)
        {
            return false;
        }

        // Place over same piece
        Vector2Int newIndices = GlobalToBoardState((int)position.x, (int)position.z);
        
        if (boardState[newIndices.x, newIndices.y] != null)
        {

            if (boardState[newIndices.x, newIndices.y].name.Contains(currentPlayer.ToString()))
            {
                return false;
            }
        }

        int underscoreIndex = selectedPiece.name.IndexOf("_");
        string pieceType = selectedPiece.name.Substring(underscoreIndex + 1);

        switch(pieceType)
        {
            case "PAWN":
                return ValidatePawnMove(position);
            case "ROOK":
                return ValidateRookMove(position);
            case "KNIGHT":
                return ValidateKnightMove(position);
            case "BISHOP":
                return ValidateBishopMove(position);
            case "KING":
                return ValidateKingMove(position);
            case "QUEEN":
                return ValidateQueenMove(position);
            default:
                return false;
        }
    }

    private void UpdateBoardState(Vector3 newPosition)
    {
        Vector2Int newIndices = GlobalToBoardState((int)newPosition.x, (int)newPosition.z);
        Vector2Int oldIndices = GlobalToBoardState((int)originalPosition.x, (int)originalPosition.z);


        bool isMoveCapture = IsMoveCapture(newPosition);

        if (isMoveCapture)
        {
            UpdateScore(boardState[newIndices.x, newIndices.y]);
        }
        else
        {
            AudioSource audioSource = moveSoundEffect.GetComponent<AudioSource>();
            audioSource.Play();
        }

        boardState[newIndices.x, newIndices.y] = selectedPiece;
        boardState[oldIndices.x, oldIndices.y] = null;

        selectedPiece = null;

        if (ignoreMovementThroughPieces) ignoreMovementThroughPieces = false;
        wasMoveMade = true;
    }

    private void UpdateScore(GameObject piece)
    {
        piece.tag = "CAPTURED";

        SpawnExplosion(piece.transform.position);

        if(piece.name.Contains("KING"))
        {
            Destroy(piece);
            isGameWon = true;
            ShowGameOverOverlay();
            return;
        }

        if (currentPlayer == Player.WHITE)
        {
            Vector3 newPosition;

            if(piece.name.Contains("PAWN"))
            {
                whiteCapturedPawns++;
                newPosition = new Vector3(8f - whiteCapturedPawns, 0.1f, 9f);

                ChessGameIntitializer.whiteScore++;
                ChessGameIntitializer.UpdateScoreWhite();
            }
            else
            {
                whiteCapturedPieces++;
                newPosition = new Vector3(8f - whiteCapturedPieces, 0.1f, 10f);

                if (piece.name.Contains("ROOK"))
                {
                    ChessGameIntitializer.whiteScore+=5;
                    ChessGameIntitializer.UpdateScoreWhite();
                } 
                else if(piece.name.Contains("KNIGHT") || piece.name.Contains("BISHOP"))
                {
                    ChessGameIntitializer.whiteScore += 3;
                    ChessGameIntitializer.UpdateScoreWhite();
                }
                else if(piece.name.Contains("QUEEN"))
                {
                    ChessGameIntitializer.whiteScore += 9;
                    ChessGameIntitializer.UpdateScoreWhite();
                }
            }

            piece.transform.localPosition = newPosition;
        }
        else
        {

            Vector3 newPosition;

            if (piece.name.Contains("PAWN"))
            {
                blackCapturedPawns++;
                newPosition = new Vector3(8f - blackCapturedPawns, 0.1f, -2f);

                ChessGameIntitializer.blackScore++;
                ChessGameIntitializer.UpdateScoreBlack();
            }
            else
            {
                blackCapturedPieces++;
                newPosition = new Vector3(8f - blackCapturedPieces, 0.1f, -3f);

                if (piece.name.Contains("ROOK"))
                {
                    ChessGameIntitializer.blackScore += 5;
                    ChessGameIntitializer.UpdateScoreBlack();
                }
                else if (piece.name.Contains("KNIGHT") || piece.name.Contains("BISHOP"))
                {
                    ChessGameIntitializer.blackScore += 3;
                    ChessGameIntitializer.UpdateScoreBlack();
                }
                else if (piece.name.Contains("QUEEN"))
                {
                    ChessGameIntitializer.blackScore += 9;
                    ChessGameIntitializer.UpdateScoreBlack();
                }
            }

            piece.transform.localPosition = newPosition;
        }
    }    


    public static Vector2Int GlobalToBoardState(int globalX, int globalZ)
    {
        int row = globalZ;
        int col = 7 - globalX;
        return new Vector2Int(row, col);
    }

    public static Vector2Int BoardStateToGlobal(int boardRow, int boardCol)
    {
        int globalX = 7 - boardCol;
        int globalZ = boardRow;
        return new Vector2Int(globalX, globalZ);
    }

    private bool ValidatePawnMove(Vector3 position)
    {

        Vector2Int originalIndices = GlobalToBoardState((int)originalPosition.x, (int)originalPosition.z);
        Vector2Int targetIndices = GlobalToBoardState((int)position.x, (int)position.z);

        if (selectedPiece.name.Contains("PAWN"))
        {

            bool invalidMovementZ;
            bool invalidMovementX;
            bool isSquareInFrontBlocked;

            if (currentPlayer == Player.WHITE)
            {
                
                if (originalPosition.z == 6)
                {
                    invalidMovementZ = originalPosition.z - position.z > 2 || originalPosition.z - position.z < 0;

                }
                else
                {
                    invalidMovementZ = originalPosition.z - position.z > 1 || originalPosition.z - position.z < 0;
                }


                invalidMovementX = position.x != originalPosition.x;
                isSquareInFrontBlocked = boardState[originalIndices.x - 1, originalIndices.y] != null;


                if(originalIndices.x - 1 >= 0 && originalIndices.y - 1 >= 0)
                {
                    if (boardState[originalIndices.x - 1, originalIndices.y - 1] != null)
                    {
                        if (!boardState[originalIndices.x - 1, originalIndices.y - 1].name.Contains(currentPlayer.ToString()))
                        {
                            if ((targetIndices.x == originalIndices.x - 1 && targetIndices.y == originalIndices.y - 1))
                            {
                                return true;
                            }
                        }
                    }
                }

                if(originalIndices.x - 1 >= 0 && originalIndices.y + 1 <= 7)
                {
                    if (boardState[originalIndices.x - 1, originalIndices.y + 1] != null)
                    {
                        if (!boardState[originalIndices.x - 1, originalIndices.y + 1].name.Contains(currentPlayer.ToString()))
                        {
                            if ((targetIndices.x == originalIndices.x - 1 && targetIndices.y == originalIndices.y + 1))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            else
            {           

                if (originalPosition.z == 1)
                {
                    invalidMovementZ = position.z - originalPosition.z > 2 || position.z - originalPosition.z < 0;

                }
                else
                {
                    invalidMovementZ = position.z - originalPosition.z > 1 || position.z - originalPosition.z < 0;
                }


                invalidMovementX = position.x != originalPosition.x;
                isSquareInFrontBlocked = boardState[originalIndices.x + 1, originalIndices.y] != null;


                if(originalIndices.y + 1 <= 7 && originalIndices.x + 1 <= 7)
                {
                    if (boardState[originalIndices.x + 1, originalIndices.y + 1] != null)
                    {
                        if (!boardState[originalIndices.x + 1, originalIndices.y + 1].name.Contains(currentPlayer.ToString()))
                        {
                            if ((targetIndices.x == originalIndices.x + 1 && targetIndices.y == originalIndices.y + 1))
                            {
                                return true;
                            }
                        }
                    }
                }

                if(originalIndices.y - 1 >= 0 && originalIndices.x + 1 <= 7)
                {
                    if (boardState[originalIndices.x + 1, originalIndices.y - 1] != null)
                    {
                        if (!boardState[originalIndices.x + 1, originalIndices.y - 1].name.Contains(currentPlayer.ToString()))
                        {
                            if ((targetIndices.x == originalIndices.x + 1 && targetIndices.y == originalIndices.y - 1))
                            {
                                return true;
                            }
                        }
                    }
                }
            }



            if (invalidMovementX || invalidMovementZ || isSquareInFrontBlocked) return false;
        }

        return true;
    }


    private bool ValidateRookMove(Vector3 position)
    {
        Vector2Int originalIndices = GlobalToBoardState((int)originalPosition.x, (int)originalPosition.z);
        Vector2Int targetIndices = GlobalToBoardState((int)position.x, (int)position.z);

        bool invalidMovement = false;
        bool isPiecePresentInBetweenMove = false;

        if (originalPosition.x != position.x && originalPosition.z != position.z)
        {
            invalidMovement = true;
        }


        if (!invalidMovement)
        {
            if (targetIndices.x > originalIndices.x)
            {
                for (int x = originalIndices.x + 1; x < targetIndices.x; x++)
                {
                    if (boardState[x, originalIndices.y] != null && !ignoreMovementThroughPieces)
                    {
                        isPiecePresentInBetweenMove = true;
                    }
                }
            }

            if (targetIndices.x < originalIndices.x)
            {
                for (int x = originalIndices.x - 1; x > targetIndices.x; x--)
                {
                    if (boardState[x, originalIndices.y] != null && !ignoreMovementThroughPieces)
                    {
                        isPiecePresentInBetweenMove = true;
                    }
                }
            }

            if (targetIndices.y > originalIndices.y)
            {
                for (int y = originalIndices.y + 1; y < targetIndices.y; y++)
                {
                    if (boardState[originalIndices.x, y] != null && !ignoreMovementThroughPieces)
                    {
                        isPiecePresentInBetweenMove = true;
                    }
                }
            }

            if (targetIndices.y < originalIndices.y)
            {
                for (int y = originalIndices.y - 1; y > targetIndices.y; y--)
                {
                    if (boardState[originalIndices.x, y] != null && !ignoreMovementThroughPieces)
                    {
                        isPiecePresentInBetweenMove = true;
                    }
                }
            }
        }

        if (invalidMovement || isPiecePresentInBetweenMove) return false;

        return true;
    }

    private bool ValidateBishopMove(Vector3 position)
    {

        Vector2Int originalIndices = GlobalToBoardState((int)originalPosition.x, (int)originalPosition.z);
        Vector2Int targetIndices = GlobalToBoardState((int)position.x, (int)position.z);


        int distanceX = Math.Abs(targetIndices.x - originalIndices.x);
        int distanceY = Math.Abs(targetIndices.y - originalIndices.y);


        if (distanceX != distanceY) return false;


        int xDirection = (targetIndices.x - originalIndices.x > 0) ? 1 : -1;
        int yDirection = (targetIndices.y - originalIndices.y > 0) ? 1 : -1;

        int steps = distanceX - 1;
        int x = originalIndices.x + xDirection;
        int y = originalIndices.y + yDirection;

        for(int i = 0; i < steps; i++)
        {
            if (boardState[x, y] != null && !ignoreMovementThroughPieces)
            {
                return false;
            }

            x += xDirection;
            y += yDirection;
        }

        return true;
    }

    private bool ValidateKingMove(Vector3 position)
    {

        if (!selectedPiece.name.Contains("KING")) return true;

        Vector2Int originalIndices = GlobalToBoardState((int)originalPosition.x, (int)originalPosition.z);
        Vector2Int targetIndices = GlobalToBoardState((int)position.x, (int)position.z);


        int distanceX = Math.Abs(targetIndices.x - originalIndices.x);
        int distanceY = Math.Abs(targetIndices.y - originalIndices.y);

        if(distanceX > 1 || distanceY > 1)
        {
            return false;
        }


        return true;
    }

    private bool ValidateQueenMove(Vector3 position)
    {
        bool isMoveLikeRook = ValidateRookMove(position);
        bool isMoveLikeBishop = ValidateBishopMove(position);

        return isMoveLikeBishop || isMoveLikeRook;
    }

    private bool ValidateKnightMove(Vector3 position)
    {
        Vector2Int originalIndices = GlobalToBoardState((int)originalPosition.x, (int)originalPosition.z);
        Vector2Int targetIndices = GlobalToBoardState((int)position.x, (int)position.z);

        int distanceX = Math.Abs(targetIndices.x - originalIndices.x);
        int distanceY = Math.Abs(targetIndices.y - originalIndices.y);

        return (distanceX == 2 && distanceY == 1) || (distanceY == 2 && distanceX == 1);
    }

    private void PulsatingImageBorderAnimation()
    {
        t += Time.deltaTime * pulseSpeed;
        float alpha = Mathf.Lerp(alphaMin, alphaMax, (Mathf.Sin(t) + 1f) / 2f);
        Color c = screenBorderImage.color;
        c.a = alpha;
        screenBorderImage.color = c;
    }

    private void SpawnExplosion(Vector3 position)
    {
        GameObject explosionInstance = Instantiate(explosionPrefab, position, Quaternion.identity);

        AudioSource audioSource = explosionInstance.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    private void ShowGameOverOverlay()
    {
        gameOverPanel.SetActive(true);
        screenBorderImage.enabled = false;

        gameOverTitle.text = "Player: " + currentPlayer + " won!";
        finalScoreWhite.text = "White Player Score: " + ChessGameIntitializer.whiteScore + " points";
        finalScoreBlack.text = "Black Player Score: " + ChessGameIntitializer.blackScore + " points";
    }

    private void SkipOpponentsTurnSpell()
    {
        if(wasMoveMade)
        {
            UpdateSpellText("Skip");
            UpdateCurrentPlayer();
            wasMoveMade = false;
        }
    }

    private void GhostMovementSpell()
    {
        if (wasMoveMade)
        {
            UpdateSpellText("Ghost");
            ignoreMovementThroughPieces = true;
            wasMoveMade = false;
        }
    }

    private void FreezeSpell()
    {
        // Implement later, an idea to freeze the clock for the current player
    }

    public async Task UpdateSpellText(string spellName)
    {
        spellDetectedText.text = "Activated " + spellName + " spell!";

        await Task.Delay(1500);

        spellDetectedText.text = "";
    }
}

