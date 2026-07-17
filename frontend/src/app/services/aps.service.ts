import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ApsConfigItem {
  APSA_ID: number;
  APSA_NOMAPS: string;
  APSA_RESOLUCION?: number | null;
  APSA_PROPIO?: number;
  APSA_SOLORELL?: number;
  APSA_ESTADO?: number;
  APSA_VIAT?: number;
  APSA_IDSUI?: number | null;
}

export interface ApsMutationPayload {
  nombre: string;
  idsui: number | null;
  resolucion: number | null;
  propio: number;
  relleno: number;
  estado: number;
  iat: number;
}

@Injectable({ providedIn: 'root' })
export class ApsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/aps`;

  constructor(private readonly http: HttpClient) {}

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('jwtOken');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'x-access-token': token || ''
    });
  }

  consultaGeneral(): Observable<ApsConfigItem[]> {
    return this.http.post<ApsConfigItem[]>(`${this.baseUrl}/consultageneral`, {}, { headers: this.getHeaders() });
  }

  consultaAps(aps: number): Observable<ApsConfigItem[]> {
    return this.http.post<ApsConfigItem[]>(`${this.baseUrl}/consultaaps`, { aps }, { headers: this.getHeaders() });
  }

  crear(payload: ApsMutationPayload): Observable<any> {
    return this.http.post(`${this.baseUrl}/crear`, payload, { headers: this.getHeaders() });
  }

  editar(id: number, payload: ApsMutationPayload): Observable<any> {
    return this.http.put(`${this.baseUrl}/editar/${id}`, payload, { headers: this.getHeaders() });
  }

  selectorPorUsuario(): Observable<ApsConfigItem[]> {
    return this.http.get<ApsConfigItem[]>(this.baseUrl, { headers: this.getHeaders() });
  }

  usuarioPorAps(aps: number): Observable<Array<{ SISU_ID: number; SISU_CORREO: string }>> {
    return this.http.post<Array<{ SISU_ID: number; SISU_CORREO: string }>>(`${this.baseUrl}/usuarioPorAPS`, { aps });
  }

  eliminar(id: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/eliminar/${id}`, { headers: this.getHeaders() });
  }

  cambiarEstado(item: ApsConfigItem, estado: number): Observable<any> {
    return this.http.put(`${this.baseUrl}/editar/${item.APSA_ID}`, {
      nombre: item.APSA_NOMAPS,
      idsui: item.APSA_IDSUI ?? null,
      resolucion: item.APSA_RESOLUCION ?? null,
      propio: item.APSA_PROPIO ?? 0,
      relleno: item.APSA_SOLORELL ?? 0,
      estado,
      iat: item.APSA_VIAT ?? 0
    }, { headers: this.getHeaders() });
  }
}
