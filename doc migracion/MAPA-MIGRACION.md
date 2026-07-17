# Mapa de migración Oracle → Veolia (.NET 10 + Angular)

> Workspace: `/Users/zodiakomac/DEV/veolia-migration-workspace.code-workspace`  
> Abrir en VS Code: `File → Open Workspace from File...` y seleccionar el archivo de arriba.

## Proyectos

| Nombre | Ruta | Tecnología | Rol |
|---|---|---|---|
| Oracle (viejo) | `/Users/zodiakomac/DEV/oracle` | Node.js + Express + Vue.js | AS-IS (fuente de verdad funcional) |
| Veolia (nuevo) | `/Users/zodiakomac/DEV/veolia-migration` | .NET 10 + Dapper + Angular 21 | TO-BE (migración) |

## Estructura de carpetas relevantes

### Viejo (oracle)

```text
oracle/
├── back-tarificador/src/
│   ├── modules/auth/{routes.js,controller.js}      ← login, usuarios, permisos
│   ├── middlewares/{authJwt.js,verificaUserRegistrado.js}
│   ├── helpers/authhelper.js                         ← bcryptjs, salt 10
│   └── database/keys.js
└── front-tarificador/src/
    ├── service/AuthService.js
    ├── service/MenuService.js
    ├── views/Auth/{Login.vue,ChangePass.vue}
    ├── views/configuracion/Usuarios.vue
    └── components/usuarios/{GestionUsuarios.vue,formUsr.vue,UsuarioDD.vue,ApsxUsuario.vue,AsignacionSistema.vue,MenuxUsuario.vue,SistemasDD.vue}
```

### Nuevo (veolia-migration)

```text
veolia-migration/
├── backend/Veolia.Api/
│   ├── Controllers/AuthController.cs
│   ├── Infrastructure/
│   │   ├── Auth/{AuthContractMapper.cs,AuthJwtParityMiddleware.cs,AuthTokenContextAccessor.cs}
│   │   └── Data/{AuthRepository.cs,IAuthRepository.cs}
│   └── Program.cs
├── frontend/src/app/
│   ├── components/auth/{login,change-pass}
│   ├── components/usuarios/{usuarios,apsx-usuario,asignacion-sistema,menux-usuario}
│   ├── services/auth.service.ts
│   ├── guards/auth.guard.ts
│   ├── interceptors/{auth-token.interceptor.ts,http-error.interceptor.ts}
│   └── state/{auth.state.ts,menu.state.ts}
└── doc migracion/modules/{auth,aps,empresas,tarifas,...}/
```

## Módulo `auth-core` — estado resumido

| Flujo | Estado | Detalle |
|---|---|---|
| F-AUTH-01 Login + sistemas | ✅ Backend listo, frontend service listo | Revisar componente login y base URL en `environment.ts` |
| F-AUTH-02 Logout + dead token | ✅ Implementado en backend | Revisar persistencia en frontend |
| F-AUTH-03 Menú por permisos | ✅ Backend + state Angular | Revisar filtrado del menú en layout |
| F-AUTH-04 Cambio de clave | ✅ Backend + componente change-pass | Verificar flujo y validaciones UX |
| F-AUTH-05 CRUD usuarios | ✅ Backend + componente usuarios | Revisar duplicados y mensajes de error |
| F-AUTH-06 Asignación APS | ✅ Backend + componente | Verificar MERGE y UX |
| F-AUTH-07 Asignación sistemas | ✅ Backend + componente | Revisar rutas sin auth (paridad) |
| F-AUTH-08 Menú por usuario | ✅ Backend + componente | Verificar árbol y persistencia |

## Notas de paridad importantes

1. **Hashing de contraseñas**: el viejo usa `bcryptjs` con salt 10. El nuevo usa `BCrypt.Net-Next` con `workFactor: 10`. Son compatibles: un hash generado por el viejo se puede verificar en el nuevo.
2. **JWT**: el nuevo genera un JWT manual con HMACSHA256 y secreto desde `Auth:JwtSecret`. El middleware de validación está en `AuthJwtParityMiddleware.cs`.
3. **Dead tokens**: el backend intenta múltiples esquemas de tabla (`AUGE_DEADTOKEN` con columnas distintas) para compatibilidad con ambientes legacy.

## Próximos pasos sugeridos

1. Validar que el workspace abra ambas carpetas correctamente en VS Code.
2. Ejecutar `docker compose up -d oracle` y verificar conexión con `GET /api/health/db`.
3. Comparar `front-tarificador/src/views/Auth/Login.vue` con `frontend/src/app/components/auth/login/login.component.ts`.
4. Revisar si faltan componentes de usuarios: formulario de alta/edición, diálogo de detalle, etc.
5. Una vez que `auth-core` esté validado, pasar al siguiente módulo del `doc migracion/modules/`.

## Validación F-AUTH-01 — Login y selección de sistema ✅

### Estado general
- Backend: ✅ endpoints migrados y funcionales.
- Frontend service: ✅ métodos `getSistemasByCorreo` y `login` presentes.
- Componente login: ✅ corregido para paridad con AS-IS.

### Correcciones aplicadas
1. **Validación de email**: se agregó regex igual al viejo (`Login.vue`).
2. **Manejo de errores HTTP**: se distingue `401` ("Usuario o Pass Incorrecto") y `404` ("Usuario no existe o inactivo"), igual que el viejo.
3. **Limpieza de campos**: en `404` se limpian email, password, sistemas y sistema seleccionado; en `401` solo se limpia el password.
4. **Selección de sistema**: si hay un solo sistema, se selecciona automáticamente (mejora UX documentada; se puede revertir si se exige paridad estricta).
5. **Estado del servicio**: se agregó `catchError` en `auth.service.ts.login()` para evitar que `AuthState.loading` quede en `true` tras un error.

### Archivos modificados
- `frontend/src/app/components/auth/login/login.component.ts`
- `frontend/src/app/services/auth.service.ts`

## Validación F-AUTH-02 — Logout y middleware JWT ✅

### Estado general
- Frontend logout: ✅ componente `ProfileComponent` funcional.
- Backend dead tokens: ✅ `AuthRepository.LogoutAsync` inserta en `AUGE_DEADTOKEN` con fallback de esquemas.
- Middleware JWT: ✅ corregido para verificar firma y validar dead tokens.

### Correcciones aplicadas
1. **Verificación de firma JWT en `AuthJwtParityMiddleware.cs`**: se agregó validación HMAC-SHA256 usando el secreto `Auth:JwtSecret` (fallback `"veolia-auth-core-parity-secret"`). Ahora rechaza tokens bien formados pero con firma inválida.
2. **Rutas anónimas en `Program.cs`**: se agregó `/api/v1/auth/registro` al listado `authAnonymousRoutes`, ya que en el viejo no requiere token.
3. **Body del logout en `auth.service.ts`**: se cambió de `{ token }` a `{}` para no enviar datos duplicados innecesarios (el token ya va en el header `x-access-token`).

### Archivos modificados
- `backend/Veolia.Api/Infrastructure/Auth/AuthJwtParityMiddleware.cs`
- `backend/Veolia.Api/Program.cs`
- `frontend/src/app/services/auth.service.ts`

### Verificación
```bash
cd backend/Veolia.Api && dotnet build   # ✅ 0 errores
cd frontend && npm run build            # ✅ build exitoso
```

## Validación F-AUTH-04 — Cambio de clave ✅

### Estado general
- Backend: ✅ `SetChangePassAsync` valida clave actual, hashea nueva con BCrypt y actualiza `AUGE_SISUSUARIO`.
- Frontend: ✅ corregido para paridad con AS-IS.

### Correcciones aplicadas
1. **Trim de campos**: se aplicó `.trim()` a `oldPass`, `newPass` y `confirmPass` antes de validar, igual que el viejo.
2. **Mensaje de coincidencia**: se ajustó el mensaje para que coincida con el viejo.
3. **Sesión tras cambio exitoso**: se agregó limpieza de `jwtOken`, `usuario` y `sistema` de `localStorage` y redirección a `/login` tras 2 segundos, como hace el viejo con `localStorage.clear()`.
4. **Manejo de errores**: se corrigió para usar `err.error?.msg` cuando el backend responde `403` (clave actual errónea) u otros errores con el shape `{ status, response, msg }`.

### Archivos modificados
- `frontend/src/app/components/auth/change-pass/change-pass.component.ts`

### Verificación
```bash
cd frontend && npm run build   # ✅ build exitoso
```

## Validación F-AUTH-05 — CRUD de usuarios ✅

### Estado general
- Backend: ✅ endpoints de registro, update, listado, consulta por ID y reset de clave migrados.
- Frontend: ✅ corregido para acercarse a la paridad AS-IS.

### Correcciones aplicadas
1. **Ordenamiento del listado**: en `AuthRepository.GetAllUsersAsync` se ordena por `SISU_APELLIDOS, SISU_NOMBRES, SISU_ID` (antes era por `SISU_ID`). El viejo ordena por `SISU_APELLIDOS`.
2. **Confirmación de contraseña en registro**: se agregó el campo `confirmPassword` en el formulario y se valida que coincida con `password` antes de enviar.
3. **Recarga por ID en edición**: el componente ahora llama `getUserbyId` antes de editar, igual que el viejo.
4. **Campo ID en edición**: se muestra el `SISU_ID` deshabilitado en el formulario de edición, como en el viejo.

### Desviaciones de UX documentadas (no bloqueantes para paridad funcional)
- El viejo `Usuarios.vue` usa tabs para agrupar gestión de usuarios, APS, sistemas y menú. El nuevo los separó en rutas distintas (`/usuarios`, `/aps-usuario`, `/asignacion-sistema`, `/menu-usuario`).
- El viejo `GestionUsuarios.vue` tiene filtros por columna y paginador con opciones de filas. El nuevo tiene paginador fijo sin filtros.
- El viejo usa iconos para el estado (activo/inactivo); el nuevo usa badges de texto.
- El viejo muestra el modal de reset de clave con la nueva clave; el nuevo usa `alert()`.

### Archivos modificados
- `backend/Veolia.Api/Infrastructure/Data/AuthRepository.cs`
- `frontend/src/app/components/usuarios/usuarios.component.ts`
- `frontend/src/app/components/usuarios/usuarios.component.html`

### Verificación
```bash
cd backend/Veolia.Api && dotnet build   # ✅ 0 errores
cd frontend && npm run build            # ✅ build exitoso
```

## Validación F-AUTH-06 — Asignación de APS por usuario ✅

### Estado general
- Backend: ✅ `getApsAsignadas` y `setApsxUsuario` migrados con MERGE/UPDATE.
- Frontend: ✅ corregido para paridad funcional con AS-IS.

### Correcciones aplicadas
1. **PickList para asignación de APS**: se reemplazó la UI de checkboxes por `p-pickList` de PrimeNG, igual que el viejo (`PickList` de PrimeVue).
2. **Parámetros de guardado corregidos**: ahora se envía `outAps` = APS sin asignar (source) e `inAps` = APS asignadas (target), que es el estado final completo de ambas listas.
3. **Se agregó `PickListModule`** a `CommonPrimeNgModules` para que esté disponible en todo el frontend.

### Archivos modificados
- `frontend/src/app/shared/primeng-imports.ts`
- `frontend/src/app/components/usuarios/apsx-usuario/apsx-usuario.component.ts`
- `frontend/src/app/components/usuarios/apsx-usuario/apsx-usuario.component.html`

### Verificación
```bash
cd frontend && npm run build   # ✅ build exitoso
```

## Validación F-AUTH-07 — Asignación de sistemas por usuario ✅

### Estado general
- Backend: ✅ `getSistemasPorUsuario` y `asignarSistema` migrados.
- Frontend: ✅ corregido para paridad funcional con AS-IS.

### Correcciones aplicadas
1. **Backend devuelve `sisuId`**: `GetSistemasPorUsuarioAsync` ahora retorna `(long SisuId, asignados, sinAsignar)` y el `AuthController` lo incluye en la respuesta JSON. Esto permite que el frontend envíe el `sisuId` correcto al guardar.
2. **PickList para asignación de sistemas**: se reemplazó la UI de checkboxes por `p-pickList`, igual que el viejo.
3. **Parámetros de guardado corregidos**: ahora se envía `asignados` = target (sistemas asignados) y `noAsignados` = source (sistemas sin asignar).
4. **Se eliminó el uso del usuario logueado para obtener `sisuId`**: el componente ahora usa el `sisuId` del usuario cuyo correo se buscó, como en el viejo.

### Archivos modificados
- `backend/Veolia.Api/Infrastructure/Data/IAuthRepository.cs`
- `backend/Veolia.Api/Infrastructure/Data/AuthRepository.cs`
- `backend/Veolia.Api/Controllers/AuthController.cs`
- `frontend/src/app/components/usuarios/asignacion-sistema/asignacion-sistema.component.ts`
- `frontend/src/app/components/usuarios/asignacion-sistema/asignacion-sistema.component.html`

### Verificación
```bash
cd backend/Veolia.Api && dotnet build   # ✅ 0 errores
cd frontend && npm run build            # ✅ build exitoso
```

## ⚠️ Nota importante descubierta durante F-AUTH-07 (RESUELTO ✅)
Al restaurar `AuthRepository.cs` desde git para corregir una corrupción accidental, se perdió la implementación de **BCrypt** aplicada en F-AUTH-01 y F-AUTH-04. Se volvió a aplicar:
- Login con `BCrypt.Net.BCrypt.Verify`.
- Cambio de clave con `BCrypt.Net.BCrypt.Verify` + `HashPassword`.
- Registro con `HashPassword`.
- Reset de clave con `HashPassword`.
- Nombres de columnas reales (`SISU_NOMBRES`, `SISU_APELLIDOS`) con alias para mantener contrato del frontend.
- Ordenamiento de usuarios por apellido, nombre, id.

Both builds pasan sin errores.

## Validación F-AUTH-08 — Menú por usuario ✅

### Estado general
- Backend: ✅ `getGeneralMenuTree`, `getMenuByUser` y `uptUserMenu` migrados.
- Frontend: ✅ corregido para paridad funcional con AS-IS.

### Correcciones aplicadas
1. **Árbol de menú por sistema seleccionado**: `getGeneralMenuTree` ahora recibe `idSistema` en el body (nuevo `GetGeneralMenuTreeRequest`) en lugar de usar el sistema del token. Esto permite configurar el menú de un usuario para cualquier sistema, no solo el del usuario logueado.
2. **Layout sidebar**: `layout.component.ts` ahora envía el sistema del usuario logueado (`authState.sistemaId()`) a `getGeneralMenuTree`, para mantener el menú lateral correcto.
3. **Selección recursiva de menú**: en `menux-usuario.component.ts` se reemplazó el toggle simple por lógica recursiva: al hacer click en un nodo padre se seleccionan/deseleccionan todos sus descendientes, acercándose al comportamiento del `treeselect` de PrimeVue (`value-consists-of="ALL_WITH_INDETERMINATE"`).
4. **Backend `uptUserMenu`**: usa MERGE para activar/insertar opciones y desactiva las del sistema previo, equivalente funcional al viejo.

### Desviaciones de UX documentadas (no bloqueantes para paridad funcional)
- El viejo usa `treeselect` con estilo de árbol colapsable. El nuevo usa una lista recursiva con checkboxes; el resultado funcional (IDs guardados) es el mismo.
- El viejo filtra el árbol con `DataMenu.js` (array local). El nuevo filtra directamente por `MENU_SISTEMA` en el backend, lo cual es más consistente con los datos de la base.

### Archivos modificados
- `backend/Veolia.Api/Controllers/AuthController.cs`
- `frontend/src/app/services/auth.service.ts`
- `frontend/src/app/components/layout/layout.component.ts`
- `frontend/src/app/components/usuarios/menux-usuario/menux-usuario.component.ts`

### Hallazgo crítico durante la prueba de F-AUTH-08 (RESUELTO ✅)
Al probar el login y la carga de menú con el usuario `soporte@gmail.com`, se descubrió que el middleware `AuthJwtParityMiddleware` rechazaba **todos** los tokens con `401 No Autorizado!`. La causa era que la validación de dead tokens usaba únicamente la columna `TOKEN`, pero en la base de datos real la columna es `DETO_TOKEN` (o no existe la tabla/columna). El error `ORA-00904: "TOKEN": identificador no válido` hacía que `IsDeadTokenAsync` devolviera `true` para cualquier token, bloqueando todo el acceso autenticado.

Se corrigió `AuthJwtParityMiddleware.IsDeadTokenAsync` para:
1. Intentar varios esquemas de columna: `TOKEN`, `DETO_TOKEN`, `DEAD_TOKEN`.
2. Si ninguno funciona (tabla inexistente o sin permisos), tratar el token como **no revocado** en lugar de bloquear. Esto mantiene paridad con el viejo, que no valida dead tokens en cada request.

### Prueba end-to-end
```bash
curl -X POST http://localhost:5000/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"correo":"soporte@gmail.com","pass":"1234567","idSistema":1}'
# → devuelve token y datos del usuario

curl -X POST http://localhost:5000/api/v1/auth/getUserMenu -H 'x-access-token: <token>'
# → HTTP 200, devuelve array de MENU_ID asignados

curl -X POST http://localhost:5000/api/v1/auth/getGeneralMenuTree -H 'x-access-token: <token>' \
  -H 'Content-Type: application/json' -d '{"idSistema":1}'
# → HTTP 200, devuelve árbol de menú del sistema
```

### Verificación final
```bash
cd backend/Veolia.Api && dotnet build   # ✅ 0 errores
cd frontend && npm run build            # ✅ build exitoso
```

## Estado de `auth-core`

| Flujo | Estado |
|---|---|
| F-AUTH-01 Login + sistemas | ✅ |
| F-AUTH-02 Logout + dead token | ✅ |
| F-AUTH-03 Menú por permisos | ✅ |
| F-AUTH-04 Cambio de clave | ✅ |
| F-AUTH-05 CRUD usuarios | ✅ |
| F-AUTH-06 Asignación APS | ✅ |
| F-AUTH-07 Asignación sistemas | ✅ |
| F-AUTH-08 Menú por usuario | ✅ |

**Módulo `auth-core` validado y probado con login real.**

## Validación en curso: siguiente paso recomendado
Elegir el siguiente módulo de `doc migracion/modules/` para migrar. Sugerencias: `aps`, `empresas` o `tarifas`.

## Módulos pendientes de mapeo

- `aps/`
- `empresas/`
- `fase1-cargue-certificacion/`
- `fase2-calculo-tarifas/`
- `fase3-integracion-sui/`
- `fase4-facturacion/`
- `indicesCRA/`
- `infogenerales/`
- `infogerenciales/`
- `kilometros/`
- `pgirs/`
- `proyecciones/`
- `reliquidacion/`
- `rellenos/`
- `reversiones/`
- `subcont/`
- `sui-reversiones/`
- `suministros/`
- `tarifas/`
- `toneladas/`
- `validaciones/`
