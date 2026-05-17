"""add psi_daily_records table

Revision ID: 21_psi_daily_records
Revises: 20_user_assignments
Create Date: 2026-05-17
"""
from alembic import op
import sqlalchemy as sa

revision = "21_psi_daily_records"
down_revision = "20_user_assignments"
branch_labels = None
depends_on = None

def upgrade():
    op.create_table(
        "psi_daily_records",
        sa.Column("id", sa.Integer, primary_key=True, autoincrement=True),
        sa.Column("part_number", sa.String(100), nullable=False, index=True),
        sa.Column("record_date", sa.Date, nullable=False, index=True),
        sa.Column("incoming_qty", sa.Float, nullable=False, server_default="0"),
        sa.Column("defect_qty", sa.Float, nullable=False, server_default="0"),
        sa.Column("note", sa.String(300), nullable=True),
        sa.Column("recorded_by", sa.Integer, sa.ForeignKey("users.id"), nullable=True),
        sa.Column("created_at", sa.DateTime, server_default=sa.func.now()),
        sa.Column("updated_at", sa.DateTime, server_default=sa.func.now()),
        sa.UniqueConstraint("part_number", "record_date", name="uq_psi_daily"),
    )

def downgrade():
    op.drop_table("psi_daily_records")
