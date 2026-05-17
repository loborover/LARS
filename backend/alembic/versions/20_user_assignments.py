"""add user_assignments table

Revision ID: 20_user_assignments
Revises: f1a8e1b9
Create Date: 2026-05-17
"""
from alembic import op
import sqlalchemy as sa

revision = "20_user_assignments"
down_revision = "f1a8e1b9"
branch_labels = None
depends_on = None

def upgrade():
    op.create_table(
        "user_assignments",
        sa.Column("id", sa.Integer, primary_key=True, autoincrement=True),
        sa.Column("user_id", sa.Integer, sa.ForeignKey("users.id", ondelete="CASCADE"), nullable=False, index=True),
        sa.Column("resource_type", sa.String(30), nullable=False, index=True),
        sa.Column("resource_key", sa.String(100), nullable=False),
        sa.UniqueConstraint("user_id", "resource_type", "resource_key", name="uq_user_resource"),
    )

def downgrade():
    op.drop_table("user_assignments")
