﻿using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
	[SerializeField] private Animator animator;
	[SerializeField] private Animator reticleAnimator;
	[SerializeField] private Bow bow;
	[SerializeField] private Arrow arrow;
	[SerializeField] private Transform playerArmsTransform;
	[SerializeField] private CharacterController characterController;
	[SerializeField] private Material material;
	[SerializeField] private MeshRenderer arrowMeshRenderer;
	[SerializeField] private TrailRenderer dashTrailRenderer;
	[SerializeField] private Text dashAmountText;
	[SerializeField] private LayerMask environmentLayerMask;
	private Coroutine drawBowCoroutine;
	private Coroutine summonArrowCoroutine;
	private const float dashForce = 1000f;
	private const float gravity = 15f;
	private const float aimSpeed = 3.5f;
	private const float summonArrowSpeed = 4f;
	private const float walkSpeed = 6f;
	private const float jumpForce = 2.5f;
	private const float crouchSpeedMultiplier = 0.7f;
	private const float standUpSpeedMultiplier = 1f;
	private const float footstepSlowSpeed = 0.45f;
	private const float footstepFastSpeed = 0.3f;
	private float currentSpeedMultiplier = 1f;
	private float firePower;
	private float moveSpeed = 6f;
	private float dashCooldown = 3;
	private float footstepCooldown;
	private float currentFootstepSpeed = 0.3f;
	private int dashAmount = 3;
	private Vector3 velocity;
	private bool isDashing;
	private bool isGrounded;
	private bool isInAir = true;
	private bool hasArrow = true;
	private bool isCrouching;
	private bool isCrouchLocked;

	public bool HasBow { get; set; } = false;
	public Vector2 movementInput { get; set; }


    void Update()
    {
		CheckGround();
		Gravity();
		Move();
		DashCooldown();
		Footsteps();
	}

    private void CheckGround()
    {
		Vector3 checkGround = new Vector3(transform.position.x, transform.position.y - characterController.height / 2, transform.position.z);
		isGrounded = Physics.Raycast(checkGround, Vector3.down, 0.1f, environmentLayerMask);
    }

    private void Gravity()
	{
		if (isGrounded && velocity.y < 0)
		{
			if (isInAir)
			{
				AudioManager.Instance.Play("Land");
				animator.SetBool("IsInAir", false);
				isInAir = false;
			}
			velocity.y = -2;
		}
		else
		{
			if (!isInAir && !isCrouchLocked)
			{
				animator.SetBool("IsInAir", true);
				isInAir = true;
			}
		}
		velocity.y -= gravity * Time.deltaTime;
		characterController.Move(velocity * Time.deltaTime);
    }

	private void Move()
	{
		if (!isDashing)
		{
			animator.SetFloat("MovementX", movementInput.x, 0.1f, Time.deltaTime);
			animator.SetFloat("MovementY", movementInput.y, 0.1f, Time.deltaTime * 1.0f);
			Vector3 move = transform.right * movementInput.x + transform.forward * movementInput.y;
			characterController.Move(move * moveSpeed * Time.deltaTime);
		}
	}

	public void Crouch()
	{
		if (isCrouching)
		{
			Vector3 checkCelling = new Vector3(transform.position.x, transform.position.y + characterController.height / 2, transform.position.z);
			bool cantStandUp = Physics.Raycast(checkCelling, Vector3.up, characterController.height, environmentLayerMask);
			if (!cantStandUp)
			{
				AudioManager.Instance.Play("StandUp");
				animator.SetTrigger("StandUp");
				characterController.height = 2.0f;
				isCrouchLocked = true;
				currentSpeedMultiplier = standUpSpeedMultiplier;
				currentFootstepSpeed = footstepFastSpeed;

				moveSpeed = walkSpeed * currentSpeedMultiplier;
				isCrouching = !isCrouching;
			}
		}
		else
		{
			AudioManager.Instance.Play("Crouch");
			animator.SetTrigger("Crouch");
			characterController.height = 1.0f;
			isCrouchLocked = true;
			currentSpeedMultiplier = crouchSpeedMultiplier;
			currentFootstepSpeed = footstepSlowSpeed;

			moveSpeed = walkSpeed * currentSpeedMultiplier;
			isCrouching = !isCrouching;
		}
	}

	public void Jump()
	{
		if (isGrounded)
		{
			AudioManager.Instance.Play("Jump");
			animator.SetTrigger("Jump");
			animator.SetBool("IsInAir", true);
			velocity.y = Mathf.Sqrt(jumpForce * 2.0f * gravity);
			isInAir = true;
			if (isCrouching)
			{
				Crouch();
			}
		}
	}

	public void Dash()
	{
		if (dashAmount > 0)
		{
			AudioManager.Instance.Play("Dash");
			animator.SetBool("IsDashing", true);
			isDashing = true;
			dashTrailRenderer.emitting = true;
			StopSummonArrow();

			Vector3 move = transform.right * movementInput.x * dashForce + transform.forward * movementInput.y * dashForce;
			if (move == Vector3.zero)
			{
				move = transform.forward * dashForce * 2;
			}
			characterController.Move(move * Time.deltaTime);
			dashAmount--;
			UIManager.Instance.SetDash(dashAmount);
		}
	}

	public void DeactivateIsDashing()
	{
		animator.SetBool("IsDashing", false);
		isDashing = false;
		dashTrailRenderer.emitting = false;
	}

	public void DeactiveIsCatching()
	{
		animator.SetBool("IsCatching", false);
	}

	public void DeactivateIsCrouchLocked()
	{
		isCrouchLocked = false;
	}

	private void DashCooldown()
	{
		if (dashAmount < 3)
		{
			dashCooldown -= Time.deltaTime;
			if (dashCooldown <= 0)
			{
				dashAmount++;
				AudioManager.Instance.Play("DashRecharge", 1f + (dashAmount / 5f));
				dashCooldown = 3f;
				UIManager.Instance.SetDash(dashAmount);
			}
		}
	}

	public void DrawBow()
	{
		if (hasArrow && HasBow)
		{
			drawBowCoroutine = StartCoroutine(DrawBowCoroutine());
		}
	}

	private IEnumerator DrawBowCoroutine()
	{
		AudioManager.Instance.Play("DrawBow");
		reticleAnimator.SetBool("IsCharging", true);
		animator.SetBool("IsCharging", true);
		moveSpeed = aimSpeed * currentSpeedMultiplier;
		currentFootstepSpeed = footstepSlowSpeed;

		float elapsedTime = 0f;
		float waitTime = 0.5f;
		float startValue = 0;
		float endValue = 1;
		while (elapsedTime < waitTime)
		{
			firePower = Mathf.Lerp(startValue, endValue, (elapsedTime / waitTime));
			animator.SetFloat("FirePower", firePower);
			elapsedTime += Time.deltaTime;
			yield return null;
		}
		firePower = endValue;
		yield return null;
	}

	public void FireArrow()
	{
		if (hasArrow && HasBow)
		{
			StopCoroutine(drawBowCoroutine);
			AudioManager.Instance.Play("FireBow");
			reticleAnimator.SetBool("IsCharging", false);
			animator.SetBool("IsCharging", false);
			moveSpeed = walkSpeed * currentSpeedMultiplier;
			currentFootstepSpeed = footstepFastSpeed;

			arrowMeshRenderer.shadowCastingMode = ShadowCastingMode.On;
			hasArrow = false;
			bow.FireArrow(firePower);

			firePower = 0f;
			animator.SetFloat("FirePower", firePower);
		}
	}

	public void StartSummonArrow()
	{
		if (!hasArrow)
		{
			summonArrowCoroutine = StartCoroutine(SummonArrowCoroutine());
		}
	}

	IEnumerator SummonArrowCoroutine()
	{
		AudioManager.Instance.Play("SummonArrow");
		animator.SetBool("IsSummoning", true);
		arrow.SetSummon(true);
		moveSpeed = summonArrowSpeed * currentSpeedMultiplier;
		currentFootstepSpeed = footstepSlowSpeed;

		float elapsedTime = 0f;
		float waitTime = 1.2f;
		float startValue = 0;
		float endValue = 0.05f;
		while (elapsedTime < waitTime)
		{
			float glowValue = Mathf.Lerp(startValue, endValue, (elapsedTime / waitTime));
			material.SetFloat("_GlowPower", glowValue);
			elapsedTime += Time.deltaTime;
			yield return null;
		}
		yield return null;
	}

	public void StopSummonArrow()
	{
		if (!hasArrow)
		{
			StopCoroutine(summonArrowCoroutine);
			AudioManager.Instance.Stop("SummonArrow");
			animator.SetBool("IsSummoning", false);
			material.SetFloat("_GlowPower", 0f);
			arrow.SetSummon(false);
		}
	}

	public void GrabArrow()
	{
		if (!hasArrow)
		{
			StopSummonArrow();
			AudioManager.Instance.Play("CatchArrow");
			animator.SetBool("IsCatching", true);
			moveSpeed = walkSpeed * currentSpeedMultiplier;
			currentFootstepSpeed = footstepFastSpeed;

			hasArrow = true;
			arrowMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			bow.EquipArrow();
		}
	}

	public void GrabBow()
	{
		if (!HasBow)
		{
			AudioManager.Instance.Play("PickUp");
			HasBow = true;
			bow.gameObject.SetActive(true);
			playerArmsTransform.gameObject.SetActive(true);
		}
	}

	private void Footsteps()
	{
		if (movementInput.magnitude > 0 && isGrounded)
		{
			footstepCooldown -= Time.deltaTime;
			if (footstepCooldown <= 0)
			{
				AudioManager.Instance.PlayRandomFromSoundGroup("Footsteps");
				footstepCooldown = currentFootstepSpeed;
			}
		}
	}
}