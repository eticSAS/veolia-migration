import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { AuthState } from '../state';

export interface LoginRequest {
  correo: string;
  pass: string;
  idSistema: number;
}

export interface LoginResponse {
  status: number;
  message: string;
  usuario?: any;
  auth_token?: string;
  sistema?: any;
}

export interface Sistema {
  SIST_ID: number;
  SIST_NOMBRE: string;
}

export interface Usuario {
  SISU_ID?: number;
  SISU_NOMBRE?: string;
  SISU_APELLIDO?: string;
  SISU_CORREO?: string;
  SISU_ESTADO?: number;
}

export interface MenuItem {
  MENU_ID: number;
  MENU_NOMBRE: string;
  MENU_PADRE?: number;
  MENU_PATH?: string;
}

export interface MenuPermission {
  MENU_ID: number;
}

export interface ApsItem {
  APSA_ID: number;
  APSA_NOMAPS: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly authState = inject(AuthState);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/auth`;

  // Acceso al estado (para nuevos componentes)
  get state() { return this.authState; }

  constructor() {
    // Hydrate desde localStorage al iniciar
    this.authState.hydrate();
  }

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('jwtOken');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'x-access-token': token || ''
    });
  }

  // F-AUTH-01: Login y sistemas
  getSistemasByCorreo(correo: string): Observable<Sistema[]> {
    return this.http.get<Sistema[]>(`${this.baseUrl}/getSistemasByCorreo?correo=${correo}`);
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    this.authState.setLoading(true);
    this.authState.setError(null);

    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request).pipe(
      tap(response => {
        this.authState.setLoading(false);
        if (response.auth_token) {
          this.authState.setToken(response.auth_token);
          this.authState.setUser(response.usuario);
          this.authState.setSistema(response.sistema);
        }
      }),
      catchError(err => {
        this.authState.setLoading(false);
        this.authState.setError(err.error?.message || 'Error de conexión');
        return throwError(() => err);
      })
    );
  }

  // F-AUTH-02: Logout
  logout(token: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/logout`, {}, {
      headers: new HttpHeaders({
        'Content-Type': 'application/json',
        'x-access-token': token
      })
    }).pipe(
      tap(() => this.authState.clearSession())
    );
  }

  // F-AUTH-03: Menú
  getUserMenu(): Observable<MenuPermission[]> {
    return this.http.post<MenuPermission[]>(`${this.baseUrl}/getUserMenu`, {}, { headers: this.getHeaders() });
  }

  // F-AUTH-04: Cambio de clave
  changePass(oldPass: string, newPass: string, confirmPass: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/setChangePass`, { oldPass, newPass, confirmPass }, { headers: this.getHeaders() });
  }

  // F-AUTH-05: CRUD Usuarios
  getAllUsers(): Observable<Usuario[]> {
    return this.http.get<Usuario[]>(`${this.baseUrl}/getAllUsers`, { headers: this.getHeaders() });
  }

  registro(usuario: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/registro`, usuario);
  }

  updateUsuario(usuario: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/updateUsuario`, usuario, { headers: this.getHeaders() });
  }

  getUserbyId(id: number): Observable<Usuario[]> {
    return this.http.post<Usuario[]>(`${this.baseUrl}/getUserbyId`, { id }, { headers: this.getHeaders() });
  }

  resetPass(id: number): Observable<string> {
    return this.http.post(`${this.baseUrl}/resetPass`, { id }, { headers: this.getHeaders(), responseType: 'text' });
  }

  // F-AUTH-06: APS
  getApsAsignadas(id: number): Observable<{ asignadas: ApsItem[], sinAsignar: ApsItem[] }> {
    return this.http.post<{ asignadas: ApsItem[], sinAsignar: ApsItem[] }>(`${this.baseUrl}/getApsAsignadas`, { id }, { headers: this.getHeaders() });
  }

  setApsxUsuario(id: number, outAps: number[], inAps: number[]): Observable<any> {
    return this.http.post(`${this.baseUrl}/setApsxUsuario`, { id, outAps, inAps }, { headers: this.getHeaders() });
  }

  // F-AUTH-07: Sistemas
  getSistemasPorUsuario(correo: string): Observable<{ asignados: Sistema[], sinAsignar: Sistema[] }> {
    return this.http.post<{ asignados: Sistema[], sinAsignar: Sistema[] }>(`${this.baseUrl}/getSistemasPorUsuario`, { correo });
  }

  asignarSistema(sisuId: number, asignados: number[], noAsignados: number[]): Observable<any> {
    return this.http.post(`${this.baseUrl}/asignarSistema`, { sisuId, asignados, noAsignados });
  }

  // F-AUTH-08: Menú por usuario
  getAllSistemas(): Observable<Sistema[]> {
    return this.http.get<Sistema[]>(`${this.baseUrl}/allSistemas`);
  }

  getGeneralMenuTree(idSistema: number): Observable<any[]> {
    return this.http.post<any[]>(`${this.baseUrl}/getGeneralMenuTree`, { idSistema }, { headers: this.getHeaders() });
  }

  getMenuByUser(idSistema: number, sisuId: number): Observable<number[]> {
    return this.http.post<number[]>(`${this.baseUrl}/getMenuByUser`, { idSistema, sisuId });
  }

  uptUserMenu(id: number, options: number[], sistema: number): Observable<any> {
    return this.http.post(`${this.baseUrl}/uptUserMenu`, { id, options, sistema }, { headers: this.getHeaders() });
  }

  getMenuUserOptions(id: number): Observable<number[]> {
    return this.http.post<number[]>(`${this.baseUrl}/getMenuUserOptions`, { id }, { headers: this.getHeaders() });
  }
}
