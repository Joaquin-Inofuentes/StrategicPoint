# Licencia de Unity para este entorno (Linux, sin pantalla)

El editor 6000.6.2f1 esta instalado en `/root/Unity/Hub/Editor/6000.6.2f1/Editor/Unity`
pero **sin licencia no abre ningun proyecto** (`No valid Unity Editor license found`, codigo 198).

## Como consigo los datos

### Opcion A - Unity Personal (gratis, sin serie)
1. Crea o usa tu cuenta en https://id.unity.com
2. Sube `Unity_v6000.6.2f1.alf` (esta carpeta) en https://license.unity3d.com/manual
3. Elige **Unity Personal** y descarga el `Unity_v6000.x.ulf` que te devuelve.
4. Pasame ese `.ulf` (o dejalo en esta carpeta) y completa `UNITY_ULF` en el formulario.

El `.alf` esta atado a esta maquina: si el contenedor se reinicia hay que generar otro
(`Unity -batchmode -nographics -quit -createManualActivationFile`).
Una licencia sacada del Hub de otra PC **no sirve**: va atada al hardware.
Si la pagina manual no ofrece Personal para tu cuenta, usa la opcion B.

### Opcion B - Pro / Plus / Enterprise
Serie (formato `XX-XXXX-XXXX-XXXX-XXXX-XXXX`): en https://id.unity.com > Subscriptions
(o te la da el administrador de tu organizacion), mas el email y la contrasena de esa cuenta.

## Cargar los datos (el "formulario")
```bash
cd Tools/Licencia
cp licencia.env.plantilla licencia.env    # completa UNITY_ULF  o  serie+email+password
./activar_licencia.sh                     # activa y abre el proyecto
./activar_licencia.sh --tests             # ademas corre la suite completa
```
`licencia.env` esta en `.gitignore`. No pegues contrasenas en el chat ni en commits.
