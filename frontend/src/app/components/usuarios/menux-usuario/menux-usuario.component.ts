import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { CommonPrimeNgModules } from '../../../shared/primeng-imports';
import { AuthService, Sistema } from '../../../services/auth.service';

@Component({
  selector: 'app-menux-usuario',
  standalone: true,
  imports: [CommonModule, FormsModule, ...CommonPrimeNgModules],
  templateUrl: './menux-usuario.component.html',
  styleUrls: ['./menux-usuario.component.css']
})
export class MenuxUsuarioComponent implements OnInit {
  usuarios: any[] = [];
  sistemas: Sistema[] = [];
  selectedUsuarioId: number | null = null;
  selectedSistemaId: number | null = null;
  menuTree: any[] = [];
  selectedMenuIds: number[] = [];
  loading = false;
  error = '';
  success = '';

  constructor(
    private authService: AuthService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadUsuarios();
    this.loadSistemas();
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

  loadSistemas(): void {
    this.authService.getAllSistemas().subscribe({
      next: (sistemas: Sistema[]) => {
        this.sistemas = sistemas;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Error al cargar sistemas';
        this.cdr.detectChanges();
      }
    });
  }

  cargarMenu(): void {
    if (!this.selectedUsuarioId || !this.selectedSistemaId) return;

    this.loading = true;
    this.error = '';

    // Cargar árbol general
    this.authService.getGeneralMenuTree(this.selectedSistemaId!).subscribe({
      next: (tree: any[]) => {
        this.menuTree = tree;
        this.cdr.detectChanges();
        // Cargar menú actual del usuario
        this.authService.getMenuByUser(this.selectedSistemaId!, this.selectedUsuarioId!).subscribe({
          next: (menuIds: number[]) => {
            this.selectedMenuIds = menuIds || [];
            this.loading = false;
            this.cdr.detectChanges();
          },
          error: (err: any) => {
            this.error = err.error?.message || 'Error al cargar menú del usuario';
            this.loading = false;
            this.cdr.detectChanges();
          }
        });
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Error al cargar árbol de menú';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  toggleMenuItem(menuId: number): void {
    const ids = this.collectDescendantIds(menuId);
    if (ids.length === 0) {
      ids.push(menuId);
    }

    const allSelected = ids.every(id => this.selectedMenuIds.includes(id));
    if (allSelected) {
      this.selectedMenuIds = this.selectedMenuIds.filter(id => !ids.includes(id));
    } else {
      for (const id of ids) {
        if (!this.selectedMenuIds.includes(id)) {
          this.selectedMenuIds.push(id);
        }
      }
    }
  }

  isSelected(menuId: number): boolean {
    const ids = this.collectDescendantIds(menuId);
    if (ids.length === 0) {
      return this.selectedMenuIds.includes(menuId);
    }
    return ids.every(id => this.selectedMenuIds.includes(id));
  }

  private collectDescendantIds(menuId: number): number[] {
    const item = this.findMenuItem(this.menuTree, menuId);
    if (!item || !item.children || item.children.length === 0) {
      return [];
    }
    const ids: number[] = [];
    this.collectIds(item.children, ids);
    return ids;
  }

  private collectIds(items: any[], ids: number[]): void {
    for (const item of items) {
      ids.push(item.id);
      if (item.children && item.children.length > 0) {
        this.collectIds(item.children, ids);
      }
    }
  }

  private findMenuItem(items: any[], menuId: number): any | null {
    for (const item of items) {
      if (item.id === menuId) {
        return item;
      }
      if (item.children && item.children.length > 0) {
        const found = this.findMenuItem(item.children, menuId);
        if (found) {
          return found;
        }
      }
    }
    return null;
  }

  get usuarioOptions(): { label: string; value: number }[] {
    return this.usuarios.map(u => ({
      label: `${u.SISU_NOMBRE} ${u.SISU_APELLIDO}`,
      value: u.SISU_ID
    }));
  }

  get sistemaOptions(): { label: string; value: number }[] {
    return this.sistemas.map(s => ({
      label: s.SIST_NOMBRE,
      value: s.SIST_ID
    }));
  }

  guardar(): void {
    if (!this.selectedUsuarioId || !this.selectedSistemaId) return;

    this.loading = true;
    this.error = '';
    this.success = '';

    this.authService.uptUserMenu(
      this.selectedUsuarioId,
      this.selectedMenuIds,
      this.selectedSistemaId
    ).subscribe({
      next: () => {
        this.success = 'Menú guardado correctamente';
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Error al guardar menú';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
