# Configuración del proyecto Unity

Guía para abrir y trabajar el proyecto `droneSim` en Mac o Windows.

## Requisitos

| Qué | Valor |
|---|---|
| Versión de Unity | **6000.6.8f1** (exacta) |
| Plantilla | Universal 3D (URP) |
| Ubicación en el repo | `unity/droneSim/` |

> Usen **exactamente** la misma versión. Si Unity Hub pregunta si quieres convertir el proyecto a otra versión, responde **no** e instala la correcta.

## Instalar Unity

1. Instalar [Unity Hub](https://unity.com/download).
2. En Unity Hub: **Installs → Install Editor** y elegir `6000.6.8f1`.
   - Si no aparece, buscarla en el [archivo de versiones](https://unity.com/releases/editor/archive) y dar **Install with Hub**.
3. No se necesitan módulos extra para trabajar en el editor (solo para generar ejecutables).

## Abrir el proyecto por primera vez

```bash
git clone https://github.com/Regina988/SistemaMultiagente.git
```

1. En Unity Hub: **Projects → Add → Add project from disk**.
2. Seleccionar la carpeta `SistemaMultiagente/unity/droneSim`.
3. La primera vez tarda varios minutos: Unity genera la carpeta `Library/` local (no se sube al repo).

## Reglas para trabajar con Git

- **Siempre subir los archivos `.meta`** junto con su asset. Si falta un `.meta` se rompen referencias en escenas y prefabs.
- **No editar la misma escena (`.unity`) al mismo tiempo.** Los conflictos en escenas son muy difíciles de resolver. Avisar en el equipo antes de tocar una escena compartida, o trabajar con prefabs / escenas separadas.
- **Cerrar Unity (o guardar todo) antes de hacer `pull`** para evitar que Unity sobrescriba cambios.
- Antes de hacer commit, revisar con `git status` que **no** aparezcan `Library/`, `Temp/`, `Logs/`, `UserSettings/` ni archivos `.sln` / `.csproj`. Ya están en `.gitignore`.
- Lo que sí se sube: `Assets/`, `Packages/` y `ProjectSettings/`.

## Configuración del editor (ya debería venir así)

En **Edit → Project Settings → Editor**:

- **Version Control → Mode:** Visible Meta Files
- **Asset Serialization → Mode:** Force Text

## Mac vs Windows

El archivo `.gitattributes` en la raíz normaliza los fines de línea (LF en el repo), así que no deberían aparecer archivos "modificados" sin cambios reales al cambiar de sistema operativo. Si alguien en Windows ve todos los archivos modificados después de clonar, ejecutar:

```bash
git config --global core.autocrlf true
git add --renormalize .
```
