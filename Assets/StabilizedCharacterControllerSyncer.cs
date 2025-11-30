using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class StabilizedCharacterControllerSyncer : MonoBehaviour
{
    public CharacterController characterController;
    public Transform cameraOffsetTransform; // Объект Camera Offset
    public Transform xrCamera; // Объект Main Camera

    // Используем FixedUpdate для синхронизации с физикой CharacterController.Move()
    void FixedUpdate()
    {
        // 1. Получаем позицию головы в локальных координатах относительно XR Origin
        // Это ключевой момент, исключающий мировую рекурсию
        Vector3 cameraLocalPosition = transform.InverseTransformPoint(xrCamera.position);

        // 2. Вычисляем желаемое локальное положение объекта Camera Offset
        // Оно должно быть противоположно смещению камеры, чтобы компенсировать его
        Vector3 desiredCameraOffsetLocalPos = Vector3.zero;
        
        // Нам нужно сместить Offset на X и Z так, чтобы камера оказалась над центром капсулы.
        // Центр капсулы находится по X/Z в (0,0) локальных координат.
        desiredCameraOffsetLocalPos.x = -cameraLocalPosition.x;
        desiredCameraOffsetLocalPos.z = -cameraLocalPosition.z;
        
        // Y-позицию Offset мы не трогаем, ее определяет Tracking Origin Mode или гравитация.

        // 3. Применяем корректировку
        // Используем MoveTowards для плавности и избегания резких скачков
        cameraOffsetTransform.localPosition = Vector3.MoveTowards(
            cameraOffsetTransform.localPosition, 
            desiredCameraOffsetLocalPos, 
            10f * Time.fixedDeltaTime // Скорость коррекции
        );
    }
}
