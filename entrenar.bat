@echo off
rem Activa el ambiente conda drone-ppo y lanza el entrenamiento PPO.
rem Cuando aparezca "Start training by pressing the Play button", presiona Play en Unity.
call "%USERPROFILE%\miniconda3\Scripts\activate.bat" drone-ppo
cd /d "%~dp0"
mlagents-learn config/drone_ppo.yaml --run-id=drone_ppo_diagnostic_04
pause
