import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { CommonPrimeNgModules } from '../../../shared/primeng-imports';
import { AuthService, Sistema } from '../../../services/auth.service';

@Component({
  selector: 'app-asignacion-sistema',
  standalone: true,
  imports: [CommonModule, FormsModule, ...CommonPrimeNgModules],
  templateUrl: './asignacion-sistema.component.html',
  styleUrls: ['./asignacion-sistema.component.css']
})
export class AsignacionSistemaComponent implements OnInit {
  correo = '';
  sisuId: number | null = null;
  listaSistema: Sistema[][] = [];
  loading = false;
  error = '';
  success = '';

  constructor(
    private authService: AuthService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {}

  buscarSistemas(): void {
    this.correo = this.correo?.trim() ?? '';
    if (!this.correo) {
      this.error = 'Ingrese un correo';
      return;
    }

    this.loading = true;
    this.error = '';
    this.success = '';
    this.sisuId = null;
    this.listaSistema = [];

    this.authService.getSistemasPorUsuario(this.correo).subscribe({
      next: (response: any) => {
        // Paridad AS-IS: PickList espera [source, target] = [sinAsignar, asignados].
        this.sisuId = response.sisuId ?? null;
        const sinAsignar = (response.sinAsignar || []) as Sistema[];
        const asignados = (response.asignados || []) as Sistema[];
        this.listaSistema = [sinAsignar, asignados];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Error al cargar sistemas';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  guardar(): void {
    if (!this.sisuId || this.listaSistema.length < 2) return;

    this.loading = true;
    this.error = '';
    this.success = '';

    // Paridad AS-IS: source = sinAsignar -> noAsignados; target = asignados -> asignados.
    const noAsignados = this.listaSistema[0].map(s => s.SIST_ID);
    const asignados = this.listaSistema[1].map(s => s.SIST_ID);

    this.authService.asignarSistema(this.sisuId, asignados, noAsignados).subscribe({
      next: () => {
        this.success = 'Asignaciones guardadas correctamente';
        this.loading = false;
        this.buscarSistemas();
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Error al guardar asignaciones';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
