import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { CommonPrimeNgModules } from '../../../shared/primeng-imports';
import { AuthService, ApsItem } from '../../../services/auth.service';

@Component({
  selector: 'app-apsx-usuario',
  standalone: true,
  imports: [CommonModule, FormsModule, ...CommonPrimeNgModules],
  templateUrl: './apsx-usuario.component.html',
  styleUrls: ['./apsx-usuario.component.css']
})
export class ApsxUsuarioComponent implements OnInit {
  usuarios: any[] = [];
  selectedUsuarioId: number | null = null;
  listaAps: ApsItem[][] = [];
  loading = false;
  error = '';
  success = '';

  get usuarioOptions(): { label: string; value: number }[] {
    return this.usuarios.map(u => ({
      label: `${u.SISU_NOMBRE} ${u.SISU_APELLIDO} (${u.SISU_CORREO})`,
      value: u.SISU_ID
    }));
  }

  constructor(
    private authService: AuthService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadUsuarios();
  }

  loadUsuarios(): void {
    this.authService.getAllUsers().subscribe({
      next: (usuarios: any[]) => {
        this.usuarios = usuarios;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Error al cargar usuarios';
        this.cdr.detectChanges();
      }
    });
  }

  onUsuarioChange(): void {
    if (!this.selectedUsuarioId) {
      this.listaAps = [];
      return;
    }

    this.loading = true;
    this.error = '';
    this.success = '';

    this.authService.getApsAsignadas(this.selectedUsuarioId).subscribe({
      next: (response: any) => {
        // Paridad AS-IS: PickList espera [source, target] = [sinAsignar, asignadas].
        const sinAsignar = (response.sinAsignar || []) as ApsItem[];
        const asignadas = (response.asignadas || []) as ApsItem[];
        this.listaAps = [sinAsignar, asignadas];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Error al cargar APS';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  guardar(): void {
    if (!this.selectedUsuarioId || this.listaAps.length < 2) return;

    this.loading = true;
    this.error = '';
    this.success = '';

    // Paridad AS-IS: source = sinAsignar -> outAps; target = asignadas -> inAps.
    const outAps = this.listaAps[0].map(a => a.APSA_ID);
    const inAps = this.listaAps[1].map(a => a.APSA_ID);

    this.authService.setApsxUsuario(this.selectedUsuarioId, outAps, inAps).subscribe({
      next: () => {
        this.success = 'Asignaciones guardadas correctamente';
        this.loading = false;
        this.onUsuarioChange();
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Error al guardar asignaciones';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
