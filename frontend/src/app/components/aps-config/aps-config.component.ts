import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CommonPrimeNgModules } from '../../shared/primeng-imports';
import { ApsService, ApsConfigItem, ApsMutationPayload } from '../../services/aps.service';
import { ApsFormComponent } from './aps-form.component';

@Component({
  selector: 'app-aps-config',
  standalone: true,
  imports: [CommonModule, ...CommonPrimeNgModules, ApsFormComponent],
  templateUrl: './aps-config.component.html',
  styleUrls: ['./aps-config.component.css']
})
export class ApsConfigComponent implements OnInit {
  apsItems: ApsConfigItem[] = [];
  loading = false;
  error = '';
  showForm = false;
  selectedAps: ApsConfigItem | null = null;

  constructor(
    private readonly apsService: ApsService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading = true;
    this.error = '';

    this.apsService.consultaGeneral().subscribe({
      next: data => {
        this.apsItems = data || [];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = err?.error?.data || 'Error al cargar APS';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  openCreate(): void {
    this.selectedAps = null;
    this.showForm = true;
  }

  openEdit(item: ApsConfigItem): void {
    this.loading = true;
    this.apsService.consultaAps(item.APSA_ID).subscribe({
      next: rows => {
        this.selectedAps = rows?.[0] ?? item;
        this.showForm = true;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = err?.error?.data || 'Error al cargar APS para edición';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  eliminar(item: ApsConfigItem): void {
    const accepted = window.confirm(`¿Seguro que querés eliminar lógicamente la APS "${item.APSA_NOMAPS}"?`);
    if (!accepted) return;

    this.loading = true;
    this.apsService.eliminar(item.APSA_ID).subscribe({
      next: () => {
        this.loadData();
      },
      error: err => {
        this.error = err?.error?.data || 'Error al eliminar APS';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  toggleEstado(item: ApsConfigItem): void {
    const nuevoEstado = item.APSA_ESTADO === 1 ? 0 : 1;
    this.apsService.cambiarEstado(item, nuevoEstado).subscribe({
      next: () => {
        item.APSA_ESTADO = nuevoEstado;
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = err?.error?.data || 'Error al cambiar estado';
        this.cdr.detectChanges();
      }
    });
  }

  closeForm(): void {
    this.showForm = false;
    this.selectedAps = null;
  }

  save(payload: ApsMutationPayload): void {
    this.loading = true;

    const request$ = this.selectedAps?.APSA_ID
      ? this.apsService.editar(this.selectedAps.APSA_ID, payload)
      : this.apsService.crear(payload);

    request$.subscribe({
      next: () => {
        this.closeForm();
        this.loadData();
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = err?.error?.data || 'Error al guardar APS';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
