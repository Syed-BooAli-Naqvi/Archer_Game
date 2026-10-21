using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArcherArrowController : MonoBehaviour
{
    [Header("Arrow Setup")]
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;
    public Transform arrowParent;

    [Header("Player Movement")]
    public Transform player;
    public float playerMoveSpeed = 5f;
    public float minPlayerX = -5f;
    public float maxPlayerX = 5f;

    [Header("Pooling")]
    public int poolSize = 50;
    public bool allowPoolGrowth = true;

    [Header("Arrow Settings")]
    public int arrowsToShoot = 1;
    public float angleBetweenArrows = 5f;
    public float extraStartRotationY = 0f;

    [Header("Aim Input Control")]
    public Camera aimCamera;
    public float minRotationY = -45f;
    public float maxRotationY = 45f;

    [Header("Shooting")]
    public float timeBetweenShots = 1f;

    private List<GameObject> arrowPool =
        new List<GameObject>();

    private Coroutine shootingCoroutine;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        CreatePool();
    }

    private void OnEnable()
    {
        ArcherGameState.OnGameStateChanged +=
            HandleGameStateChanged;
    }

    private void OnDisable()
    {
        ArcherGameState.OnGameStateChanged -=
            HandleGameStateChanged;

        StopShooting();
    }

    private void Start()
    {
        if (ArcherGameState.IsGameRunning)
        {
            StartShooting();
        }
    }

    private void Update()
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        HandleAimInput();
    }

    private void HandleGameStateChanged(bool isRunning)
    {
        if (isRunning)
        {
            StartShooting();
        }
        else
        {
            StopShooting();
        }
    }

    private void HandleAimInput()
    {
        // Touch
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // Tapping the booster button must not steer the archer.
            if (BoosterPickup.IsOverButton(touch.position))
                return;

            if (touch.phase == TouchPhase.Began ||
                touch.phase == TouchPhase.Moved ||
                touch.phase == TouchPhase.Stationary)
            {
                ApplyPointerPosition(touch.position);
            }

            return;
        }

        // Mouse
        if (Input.GetMouseButton(0) && !BoosterPickup.IsOverButton(Input.mousePosition))
        {
            ApplyPointerPosition(Input.mousePosition);
        }
    }

    private void ApplyPointerPosition(Vector2 pointerPosition)
    {
        if (aimCamera == null ||
            arrowSpawnPoint == null)
        {
            return;
        }

        // ---------------------------------
        // PLAYER LEFT / RIGHT MOVEMENT
        // ---------------------------------

        MovePlayer(pointerPosition);

        // // ---------------------------------
        // // ARROW AIM
        // // ---------------------------------

        // Plane aimPlane =
        //     new Plane(
        //         Vector3.up,
        //         arrowSpawnPoint.position
        //     );

        // Ray ray =
        //     aimCamera.ScreenPointToRay(
        //         pointerPosition
        //     );

        // if (!aimPlane.Raycast(
        //         ray,
        //         out float distance))
        // {
        //     return;
        // }

        // Vector3 worldPoint =
        //     ray.GetPoint(distance);

        // Vector3 direction =
        //     worldPoint -
        //     arrowSpawnPoint.position;

        // direction.y = 0f;

        // if (direction.sqrMagnitude < 0.0001f)
        //     return;

        // float angle =
        //     Vector3.SignedAngle(
        //         Vector3.forward,
        //         direction,
        //         Vector3.up
        //     );

        // extraStartRotationY =
        //     Mathf.Clamp(
        //         angle,
        //         minRotationY,
        //         maxRotationY
        //     );
    }

    private void MovePlayer(Vector2 pointerPosition)
    {
        if (player == null)
            return;

        // Convert screen X into a 0-1 value.
        float normalizedX =
            pointerPosition.x /
            Screen.width;

        // Convert 0-1 into player X range.
        float targetX =
            Mathf.Lerp(
                minPlayerX,
                maxPlayerX,
                normalizedX
            );

        Vector3 position =
            player.position;

        position.x =
            Mathf.Lerp(
                position.x,
                targetX,
                playerMoveSpeed * Time.deltaTime
            );

        position.x =
            Mathf.Clamp(
                position.x,
                minPlayerX,
                maxPlayerX
            );

        player.position = position;
    }

    private void CreatePool()
    {
        if (arrowPrefab == null)
            return;

        for (int i = 0; i < poolSize; i++)
        {
            CreateArrow();
        }
    }

    private GameObject CreateArrow()
    {
        GameObject arrow =
            Instantiate(
                arrowPrefab,
                arrowParent
            );

        arrow.SetActive(false);

        ArrowController controller =
            arrow.GetComponent<ArrowController>();

        if (controller != null)
            controller.SetPool(this);

        arrowPool.Add(arrow);

        return arrow;
    }

    public void StartShooting()
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        if (shootingCoroutine != null)
            return;

        shootingCoroutine =
            StartCoroutine(
                ShootingRoutine()
            );
    }

    public void StopShooting()
    {
        if (shootingCoroutine == null)
            return;

        StopCoroutine(shootingCoroutine);
        shootingCoroutine = null;
    }

    private IEnumerator ShootingRoutine()
    {
        while (ArcherGameState.IsGameRunning)
        {
            Shoot();

            yield return new WaitForSeconds(
                timeBetweenShots
            );
        }

        shootingCoroutine = null;
    }

    private void Shoot()
    {
        ShootVolley(arrowsToShoot);
    }

    /// <summary>Booster button: fires 'volleys' extra volleys of 'arrowsPerVolley' arrows, 'gap' seconds apart.</summary>
    public void FireBoostVolleys(int volleys, int arrowsPerVolley, float gap)
    {
        StartCoroutine(BoostVolleysRoutine(volleys, arrowsPerVolley, gap));
    }

    private IEnumerator BoostVolleysRoutine(int volleys, int arrowsPerVolley, float gap)
    {
        for (int v = 0; v < volleys; v++)
        {
            ShootVolley(arrowsPerVolley);
            yield return new WaitForSeconds(gap);
        }
    }

    private void ShootVolley(int arrowsToShoot)
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        if (arrowPrefab == null ||
            arrowSpawnPoint == null)
        {
            return;
        }

        if (arrowsToShoot <= 0)
            return;


        if (SoundManager.Instance)
            SoundManager.Instance.PlaySound(SoundName.ArrowShoot);

        for (int i = 0;
             i < arrowsToShoot;
             i++)
        {
            float angle = 0f;

            if (arrowsToShoot > 1)
            {
                float totalSpread =
                    (arrowsToShoot - 1) *
                    angleBetweenArrows;

                angle =
                    -totalSpread * 0.5f +
                    i * angleBetweenArrows;
            }

            angle += extraStartRotationY;

            Vector3 direction =
                Quaternion.Euler(
                    0f,
                    angle,
                    0f
                ) * Vector3.forward;

            Quaternion rotation =
                Quaternion.LookRotation(
                    direction
                );

            GameObject arrow =
                GetArrow();

            if (arrow == null)
                continue;

            arrow.transform.SetPositionAndRotation(
                arrowSpawnPoint.position,
                rotation
            );

            arrow.SetActive(true);
        }
    }

    private GameObject GetArrow()
    {
        for (int i = 0;
             i < arrowPool.Count;
             i++)
        {
            if (!arrowPool[i].activeInHierarchy)
                return arrowPool[i];
        }

        if (allowPoolGrowth &&
            arrowPrefab != null)
        {
            return CreateArrow();
        }

        return null;
    }

    public void ReturnArrow(GameObject arrow)
    {
        if (arrow == null)
            return;

        arrow.SetActive(false);
    }

    public void AddArrow()
    {
        arrowsToShoot++;
    }

    public void AddArrow(int count)
    {
        arrowsToShoot =
            Mathf.Max(
                1,
                arrowsToShoot + count
            );
    }
}