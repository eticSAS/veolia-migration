import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { CommonPrimeNgModules } from '../../../shared/primeng-imports';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-change-pass',
  standalone: true,
  imports: [CommonModule, FormsModule, ...CommonPrimeNgModules],
  templateUrl: './change-pass.component.html',
  styleUrls: ['./change-pass.component.css']
})
export class ChangePassComponent {
  oldPass = '';
  newPass = '';
  confirmPass = '';
  error = '';
  success = '';
  loading = false;

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  changePassword(): void {
    this.oldPass = this.oldPass?.trim() ?? '';
    this.newPass = this.newPass?.trim() ?? '';
    this.confirmPass = this.confirmPass?.trim() ?? '';

    if (!this.oldPass || !this.newPass || !this.confirmPass) {
      this.error = 'Complete todos los campos';
      return;
    }

    if (this.newPass !== this.confirmPass) {
      this.error = 'El campo de "Nueva Clave" y "Confirmar Nueva Clave" no son iguales';
      return;
    }

    this.loading = true;
    this.error = '';
    this.success = '';

    this.authService.changePass(this.oldPass, this.newPass, this.confirmPass).subscribe({
      next: (response) => {
        this.loading = false;

        if (response.status === 200) {
          this.success = response.msg || 'Contraseña actualizada correctamente';
          // Paridad AS-IS: el viejo limpia localStorage tras cambio exitoso.
          setTimeout(() => {
            localStorage.removeItem('jwtOken');
            localStorage.removeItem('usuario');
            localStorage.removeItem('sistema');
            this.router.navigate(['/login']);
          }, 2000);
        } else {
          this.error = response.msg || 'Error al cambiar contraseña';
        }
      },
      error: (err) => {
        this.loading = false;

        // El backend devuelve { status, response, msg } con HTTP 403/500.
        if (err.status === 403) {
          this.error = err.error?.msg || 'Clave Actual Erronea';
          this.oldPass = '';
        } else {
          this.error = err.error?.msg || err.error?.message || 'Error de conexión';
        }
      }
    });
  }
}
